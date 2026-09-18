using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Mcp;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MailManager.Api.Tests;

public sealed class McpIntegrationTests : IClassFixture<McpApplication>
{
    private readonly McpApplication app;
    public McpIntegrationTests(McpApplication app) => this.app = app;

    [Fact]
    public async Task Anonymous_requests_advertise_public_oauth_metadata()
    {
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=\"http://localhost:8080/api/mcp/oauth-protected-resource\"", response.Headers.WwwAuthenticate.ToString());
        var metadata = await client.GetFromJsonAsync<JsonElement>("/api/mcp/oauth-protected-resource");
        Assert.Equal(McpApplication.Resource, metadata.GetProperty("resource").GetString());
        Assert.Equal(McpApplication.Issuer, metadata.GetProperty("authorization_servers")[0].GetString());
    }

    [Theory]
    [InlineData("mail-manager-api", "mailmanager", null, false, 401)]
    [InlineData("http://localhost:8080/api/mcp", "", null, false, 403)]
    [InlineData("http://localhost:8080/api/mcp", "mailmanager", "automation", false, 403)]
    [InlineData("http://localhost:8080/api/mcp", "mailmanager", null, true, 401)]
    public async Task Rejects_wrong_audience_missing_scope_service_accounts_and_expired_tokens(string audience, string scope, string? role, bool expired, int status)
    {
        using var client = app.Client("alice", audience, scope, role, expired);
        using var request = Request("tools/list", new { });
        var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Fact]
    public async Task Mcp_tokens_cannot_call_the_general_application_api()
    {
        using var client = app.Client("alice");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/mailboxes")).StatusCode);
    }

    [Fact]
    public async Task Unexpected_browser_origins_are_rejected()
    {
        using var client = app.Client("alice");
        using var request = Request("tools/list", new { });
        request.Headers.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("2025-03-26")]
    [InlineData("2025-06-18")]
    [InlineData("2025-11-25")]
    public async Task Initialize_and_discover_six_tools_with_write_annotations(string version)
    {
        using var client = app.Client("alice");
        var init = await Rpc(client, "initialize", new { protocolVersion = version, capabilities = new { }, clientInfo = new { name = "interop-test", version = "1.0" } });
        Assert.True(init.GetProperty("result").TryGetProperty("protocolVersion", out _));
        var result = await Rpc(client, "tools/list", new { });
        var tools = result.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        Assert.Equal(6, tools.Length);
        var apply = tools.Single(x => x.GetProperty("name").GetString() == "apply_configuration_proposal");
        Assert.False(apply.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.True(apply.GetProperty("annotations").GetProperty("idempotentHint").GetBoolean());
        Assert.Contains("proposalId", apply.GetProperty("inputSchema").ToString());
        var prepare = tools.Single(x => x.GetProperty("name").GetString() == "prepare_configuration_changes");
        Assert.Contains("All", prepare.GetProperty("inputSchema").ToString());
    }

    [Fact]
    public async Task Prepare_simulate_apply_and_retry_preserve_exact_changes()
    {
        var (owner, mailboxId) = await app.Seed();
        using var client = app.Client(owner);
        var prepared = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        var proposalId = prepared.GetProperty("proposalId").GetGuid();
        Assert.Equal("pending_confirmation", prepared.GetProperty("status").GetString());
        var current = await Call(client, "get_configuration", new { mailboxId });
        Assert.Empty(current.GetProperty("rules").EnumerateArray());
        var simulation = await Call(client, "simulate_configuration", new { mailboxId, proposalId, sender = "compta@fournisseur.fr", subject = "Facture septembre" });
        Assert.True(simulation.GetProperty("isClassified").GetBoolean());
        var applied = await Call(client, "apply_configuration_proposal", new { proposalId });
        Assert.Equal("applied", applied.GetProperty("status").GetString());
        var replay = await Call(client, "apply_configuration_proposal", new { proposalId });
        Assert.Equal(applied.GetProperty("appliedAt").GetString(), replay.GetProperty("appliedAt").GetString());
        current = await Call(client, "get_configuration", new { mailboxId });
        Assert.Single(current.GetProperty("rules").EnumerateArray());
        Assert.Single(current.GetProperty("destinations").EnumerateArray());
        Assert.Equal(prepared.GetProperty("after").GetProperty("rules")[0].GetProperty("id").GetGuid(),
            current.GetProperty("rules")[0].GetProperty("id").GetGuid());
        await app.WithDb(async db => Assert.False(await db.ProcessingLogs.AnyAsync(x => x.MailboxConnectionId == mailboxId)));
    }

    [Fact]
    public async Task Users_cannot_read_prepare_or_apply_other_users_configuration()
    {
        var (owner, mailboxId) = await app.Seed();
        using var client = app.Client(owner);
        var prepared = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        using var other = app.Client("other-" + owner);
        await ToolError(other, "get_configuration", new { mailboxId });
        await ToolError(other, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        await ToolError(other, "apply_configuration_proposal", new { proposalId = prepared.GetProperty("proposalId").GetGuid() });
        var list = await Call(other, "list_mailboxes", new { });
        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task Demo_can_read_and_simulate_but_cannot_prepare_or_apply()
    {
        var (owner, mailboxId) = await app.Seed();
        using var client = app.Client(owner);
        var prepared = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        using var demo = app.Client(owner, role: "demo");
        await Call(demo, "get_configuration", new { mailboxId });
        await Call(demo, "simulate_configuration", new { mailboxId, sender = "demo@example.com" });
        await ToolError(demo, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        await ToolError(demo, "apply_configuration_proposal", new { proposalId = prepared.GetProperty("proposalId").GetGuid() });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expired_or_stale_proposals_do_not_change_configuration(bool expired)
    {
        var (owner, mailboxId) = await app.Seed();
        using var client = app.Client(owner);
        var prepared = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        var proposalId = prepared.GetProperty("proposalId").GetGuid();
        await app.WithDb(async db =>
        {
            if (expired) (await db.ConfigurationProposals.FindAsync(proposalId))!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            else db.LabelDefinitions.Add(new LabelDefinition { Id = Guid.NewGuid(), MailboxConnectionId = mailboxId, Name = "Manuel" });
            await db.SaveChangesAsync();
        });
        await ToolError(client, "apply_configuration_proposal", new { proposalId });
        await app.WithDb(async db => Assert.False(await db.ClassificationRules.AnyAsync(x => x.MailboxConnectionId == mailboxId)));
    }

    [Fact]
    public async Task Invalid_batch_is_rejected_without_saving_a_proposal()
    {
        var (owner, mailboxId) = await app.Seed();
        using var client = app.Client(owner);
        var changes = Changes();
        changes = changes with { Rules = [changes.Rules[0] with { DestinationKey = Guid.NewGuid().ToString() }] };
        await ToolError(client, "prepare_configuration_changes", new { mailboxId, changes });
        await app.WithDb(async db => Assert.False(await db.ConfigurationProposals.AnyAsync(x => x.MailboxConnectionId == mailboxId)));
    }

    [Fact]
    public async Task Existing_rules_can_be_updated_and_disabled_without_duplication()
    {
        var (owner, mailboxId) = await app.Seed();
        using var client = app.Client(owner);
        var p = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        await Call(client, "apply_configuration_proposal", new { proposalId = p.GetProperty("proposalId").GetGuid() });
        var rule = p.GetProperty("after").GetProperty("rules")[0];
        var change = Changes().Rules[0] with { Id = rule.GetProperty("id").GetGuid(), IsActive = false, DestinationKey = rule.GetProperty("destinationId").GetString()! };
        p = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = new ConfigurationChanges([], [change]) });
        await Call(client, "apply_configuration_proposal", new { proposalId = p.GetProperty("proposalId").GetGuid() });
        var result = await Call(client, "simulate_configuration", new { mailboxId, sender = "compta@fournisseur.fr", subject = "facture" });
        Assert.False(result.GetProperty("isClassified").GetBoolean());
    }

    private static ConfigurationChanges Changes() => new(
        [new("compta", null, "Comptabilité", "#2563eb", true)],
        [new(null, "Factures fournisseur", "compta", 10, true, MatchMode.All, ["compta@fournisseur.fr"], [], ["facture"], [])]);

    [Fact]
    public async Task Provider_failure_is_reported_without_recreating_configuration_on_retry()
    {
        var (owner, mailboxId) = await app.Seed();
        await app.WithDb(async db =>
        {
            (await db.MailboxConnections.FindAsync(mailboxId))!.EncryptedRefreshToken = "fake-test-token-never-exposed";
            await db.SaveChangesAsync();
        });
        using var client = app.Client(owner);
        var p = await Call(client, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        var id = p.GetProperty("proposalId").GetGuid();
        var destinationId = p.GetProperty("changedDestinationIds")[0].GetGuid();
        var applied = await Call(client, "apply_configuration_proposal", new { proposalId = id });
        Assert.Equal("applied", applied.GetProperty("status").GetString());
        Assert.Equal("failed", applied.GetProperty("synchronizationStatus").GetString());
        Assert.True(applied.GetProperty("warnings").GetArrayLength() > 1);
        await Call(client, "apply_configuration_proposal", new { proposalId = id });
        Assert.Equal(1, FailingMcpProvider.Calls[destinationId]);
        Assert.DoesNotContain("fake-test-token", applied.ToString());
    }

    [Fact]
    public async Task Concurrent_application_has_one_effect_and_can_be_read_afterwards()
    {
        var (owner, mailboxId) = await app.Seed();
        using var first = app.Client(owner);
        using var second = app.Client(owner);
        var p = await Call(first, "prepare_configuration_changes", new { mailboxId, changes = Changes() });
        var proposalId = p.GetProperty("proposalId").GetGuid();
        var responses = await Task.WhenAll(new[] { first, second }.Select(client => Rpc(client, "tools/call",
            new { name = "apply_configuration_proposal", arguments = new { proposalId } })));
        Assert.Contains(responses, response => response.TryGetProperty("result", out var r)
            && (!r.TryGetProperty("isError", out var error) || !error.GetBoolean()));
        var result = await Call(first, "get_configuration_proposal", new { proposalId });
        Assert.Equal("applied", result.GetProperty("status").GetString());
        await app.WithDb(async db =>
        {
            Assert.Equal(1, await db.ClassificationRules.CountAsync(x => x.MailboxConnectionId == mailboxId));
            Assert.Equal(1, await db.LabelDefinitions.CountAsync(x => x.MailboxConnectionId == mailboxId));
        });
    }

    internal static HttpRequestMessage Request(string method, object args) => new(HttpMethod.Post, "/api/mcp")
    {
        Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = args }, options: McpJson.Options),
        Headers = { { "Accept", "application/json, text/event-stream" }, { "MCP-Protocol-Version", "2025-11-25" } }
    };
    internal static async Task<JsonElement> Rpc(HttpClient client, string method, object args)
    {
        using var request = Request(method, args);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            text = text.Split('\n').Last(x => x.StartsWith("data: "))[6..];
        return JsonSerializer.Deserialize<JsonElement>(text);
    }
    internal static async Task<JsonElement> Call(HttpClient client, string name, object arguments)
    {
        var rpc = await Rpc(client, "tools/call", new { name, arguments });
        Assert.False(rpc.TryGetProperty("error", out _), rpc.ToString());
        var result = rpc.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var error) && error.GetBoolean(), result.ToString());
        return JsonSerializer.Deserialize<JsonElement>(result.GetProperty("content")[0].GetProperty("text").GetString()!);
    }
    internal static async Task ToolError(HttpClient client, string name, object arguments)
    {
        var rpc = await Rpc(client, "tools/call", new { name, arguments });
        Assert.True(rpc.GetProperty("result").GetProperty("isError").GetBoolean(), rpc.ToString());
    }
}

public sealed class McpApplication : WebApplicationFactory<Program>
{
    public const string Resource = "http://localhost:8080/api/mcp";
    public const string Issuer = "http://localhost:8081/realms/mail-manager";
    private readonly string database = Guid.NewGuid().ToString();
    private readonly SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes("mcp-integration-tests-only-signing-key-1234567890"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES") ?? "Host=localhost;Database=unused;Username=unused;Password=unused");
        builder.UseSetting("Database:ApplyMigrations", "false");
        builder.UseSetting("Mcp:PublicUrl", Resource);
        builder.ConfigureServices(services =>
        {
            if (Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES") is null)
            {
                services.RemoveAll<DbContextOptions<MailManagerDbContext>>();
                services.RemoveAll<MailManagerDbContext>();
                services.AddDbContext<MailManagerDbContext>(options => options.UseInMemoryDatabase(database));
            }
            var cleanup = services.SingleOrDefault(x => x.ImplementationType == typeof(DataRetentionCleanupService));
            if (cleanup is not null) services.Remove(cleanup);
            services.RemoveAll<IMailboxProviderAdapter>();
            services.AddSingleton<IMailboxProviderAdapter>(new FailingMcpProvider(MailProvider.Gmail));
            services.AddSingleton<IMailboxProviderAdapter>(new FailingMcpProvider(MailProvider.Outlook));
            foreach (var scheme in new[] { McpOptions.Scheme, JwtBearerDefaults.AuthenticationScheme })
                services.PostConfigure<JwtBearerOptions>(scheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    configuration.SigningKeys.Add(key);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
        });
    }
    public HttpClient Client(string subject, string audience = Resource, string scope = "mailmanager", string? role = null, bool expired = false)
    {
        var claims = new List<Claim> { new("sub", subject), new("scope", scope) };
        if (role is not null) claims.Add(new("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } })));
        var token = new JwtSecurityToken(Issuer, audience, claims, DateTime.UtcNow.AddHours(-2),
            expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1), new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    public async Task<(string Owner, Guid MailboxId)> Seed()
    {
        var owner = Guid.NewGuid().ToString();
        var id = Guid.NewGuid();
        await WithDb(async db =>
        {
            if (db.Database.IsRelational()) await db.Database.MigrateAsync();
            db.MailboxConnections.Add(new MailboxConnection { Id = id, OwnerSubject = owner, DisplayName = "Test MCP", Provider = MailProvider.Gmail });
            await db.SaveChangesAsync();
        });
        return (owner, id);
    }
    public async Task WithDb(Func<MailManagerDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<MailManagerDbContext>());
    }
}

public sealed class FailingMcpProvider(MailProvider provider) : IMailboxProviderAdapter
{
    public static ConcurrentDictionary<Guid, int> Calls { get; } = new();
    public MailProvider Provider => provider;
    public Task<bool> SynchronizeDestinationAsync(Guid labelDefinitionId, CancellationToken cancellationToken)
    {
        Calls.AddOrUpdate(labelDefinitionId, 1, (_, value) => value + 1);
        return Task.FromResult(false);
    }
    public Task<MailboxConnectionTestResponse?> TestConnectionAsync(Guid mailboxConnectionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<MailboxSyncResponse?> SyncAsync(Guid mailboxConnectionId, int maxResults, CancellationToken cancellationToken) => throw new NotSupportedException();
}
