using System.Security.Claims;
using System.Text.Json;

namespace MailManager.Api.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    public string Subject => Principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Le jeton authentifié ne contient pas de claim sub.");

    public string DisplayName => Principal.FindFirstValue("name")
        ?? Principal.FindFirstValue("preferred_username")
        ?? "Utilisateur";

    public bool IsDemo => Principal.HasRealmRole("demo");
    public bool IsAutomation => Principal.HasRealmRole("automation");

    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("Aucun contexte HTTP authentifié n'est disponible.");
}

public static class ClaimsPrincipalExtensions
{
    public static bool HasRealmRole(this ClaimsPrincipal principal, string role)
    {
        var realmAccess = principal.FindFirstValue("realm_access");
        if (string.IsNullOrWhiteSpace(realmAccess)) return false;

        try
        {
            using var document = JsonDocument.Parse(realmAccess);
            return document.RootElement.TryGetProperty("roles", out var roles)
                && roles.ValueKind == JsonValueKind.Array
                && roles.EnumerateArray().Any(item =>
                    string.Equals(item.GetString(), role, StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

