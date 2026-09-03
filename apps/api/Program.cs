using System.Text.Json.Serialization;
using MailManager.Api.Data;
using MailManager.Api.Services;
using MailManager.Api.Configuration;
using MailManager.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection(AuthenticationOptions.SectionName));
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection(DataRetentionOptions.SectionName));
var authenticationOptions = builder.Configuration
    .GetSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>()
    ?? throw new InvalidOperationException("Authentication configuration is required.");
if (string.IsNullOrWhiteSpace(authenticationOptions.MetadataAddress)
    || string.IsNullOrWhiteSpace(authenticationOptions.Issuer)
    || string.IsNullOrWhiteSpace(authenticationOptions.Audience))
{
    throw new InvalidOperationException("Authentication MetadataAddress, Issuer and Audience are required.");
}
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<MailboxAccessService>();
builder.Services.AddScoped<AccountDataService>();
builder.Services.AddHostedService<DataRetentionCleanupService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.MetadataAddress = authenticationOptions.MetadataAddress;
        options.RequireHttpsMetadata = authenticationOptions.RequireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authenticationOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authenticationOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(AuthorizationPolicies.Automation, policy =>
        policy.RequireAssertion(context => context.User.HasRealmRole("automation")));
});
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddCors(options => options.AddPolicy("Web", policy =>
    policy.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Content-Disposition")));

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

app.UseForwardedHeaders();
app.UseCors("Web");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();

public partial class Program;
