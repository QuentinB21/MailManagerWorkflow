using System.Text.Json;

namespace MailManager.Api.Tests;

public sealed class KeycloakMcpConfigurationTests
{
    [Fact]
    public void Missing_activation_field_cannot_silently_disable_a_destination()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MailManager.Api.Mcp.DestinationChange>(
            "{\"key\":\"a\",\"id\":null,\"name\":\"Comptabilité\",\"color\":null}", MailManager.Api.Mcp.McpJson.Options));
    }

    [Fact]
    public void Realm_preserves_standard_scopes_and_restricts_mcp_clients()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "keycloak-realm.json")));
        var realm = document.RootElement;
        var scopes = realm.GetProperty("clientScopes").EnumerateArray().ToDictionary(x => x.GetProperty("name").GetString()!);
        foreach (var scope in realm.GetProperty("defaultDefaultClientScopes").EnumerateArray())
            Assert.True(scopes.ContainsKey(scope.GetString()!));
        Assert.Contains(realm.GetProperty("defaultDefaultClientScopes").EnumerateArray(), x => x.GetString() == "roles");
        Assert.Contains(realm.GetProperty("defaultDefaultClientScopes").EnumerateArray(), x => x.GetString() == "basic");
        foreach (var id in new[] { "mail-manager-chatgpt", "mail-manager-claude" })
        {
            var client = realm.GetProperty("clients").EnumerateArray().Single(x => x.GetProperty("clientId").GetString() == id);
            Assert.True(client.GetProperty("publicClient").GetBoolean());
            Assert.True(client.GetProperty("consentRequired").GetBoolean());
            Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
            Assert.False(client.GetProperty("serviceAccountsEnabled").GetBoolean());
            Assert.False(client.GetProperty("fullScopeAllowed").GetBoolean());
            Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
            Assert.DoesNotContain(client.GetProperty("redirectUris").EnumerateArray(), x => x.GetString()!.Contains('*'));
            var mapping = realm.GetProperty("scopeMappings").EnumerateArray().Single(x => x.GetProperty("client").GetString() == id);
            Assert.Contains(mapping.GetProperty("roles").EnumerateArray(), x => x.GetString() == "demo");
            Assert.Contains(mapping.GetProperty("roles").EnumerateArray(), x => x.GetString() == "automation");
        }
        foreach (var mapper in scopes["mailmanager"].GetProperty("protocolMappers").EnumerateArray())
            Assert.EndsWith("/api/mcp", mapper.GetProperty("config").GetProperty("included.custom.audience").GetString());
    }
}
