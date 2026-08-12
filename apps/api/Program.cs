using System.Text.Json.Serialization;
using MailManager.Api.Data;
using MailManager.Api.Services;
using MailManager.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
}

builder.Services.AddDbContext<MailManagerDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection(GmailOptions.SectionName));
builder.Services.Configure<OutlookOptions>(builder.Configuration.GetSection(OutlookOptions.SectionName));
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("MailManagerWorkflow");
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
builder.Services.AddHttpClient("GoogleOAuth", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("GmailApi", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("MicrosoftOAuth", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("MicrosoftGraph", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<ClassificationEngine>();
builder.Services.AddScoped<EmailProcessingService>();
builder.Services.AddScoped<GmailTokenProtector>();
builder.Services.AddScoped<GmailOAuthConfigurationService>();
builder.Services.AddScoped<GmailOAuthService>();
builder.Services.AddScoped<GmailMailboxService>();
builder.Services.AddScoped<OutlookTokenProtector>();
builder.Services.AddScoped<OutlookOAuthService>();
builder.Services.AddScoped<OutlookMailboxService>();
builder.Services.AddScoped<IMailboxProviderAdapter>(provider => provider.GetRequiredService<GmailMailboxService>());
builder.Services.AddScoped<IMailboxProviderAdapter>(provider => provider.GetRequiredService<OutlookMailboxService>());
builder.Services.AddScoped<MailboxProviderResolver>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Web", policy =>
    policy.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MailManagerDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Web");
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program;
