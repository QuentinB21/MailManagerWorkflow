using System.Text.Json;

namespace MailManager.Api.Services;

public static class ProviderAuthenticationFailureDetector
{
    public static bool RequiresReconnect(Exception exception)
    {
        var details = exception switch
        {
            GmailApiException gmail => gmail.ProviderDetails,
            OutlookApiException outlook => outlook.ProviderDetails,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(details)) return false;

        try
        {
            using var document = JsonDocument.Parse(details);
            var error = document.RootElement.TryGetProperty("error", out var value) ? value : default;
            if (error.ValueKind == JsonValueKind.String)
            {
                return IsReconnectCode(error.GetString());
            }

            if (error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String)
            {
                return IsReconnectCode(code.GetString());
            }
        }
        catch (JsonException)
        {
            // Some provider endpoints return a non-JSON OAuth error body.
        }

        return details.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase)
            || details.Contains("InvalidAuthenticationToken", StringComparison.OrdinalIgnoreCase);
    }

    public static string ReconnectMessage(string provider) =>
        $"L’autorisation {provider} a expiré ou a été révoquée. Reconnectez cette boîte.";

    private static bool IsReconnectCode(string? code) =>
        string.Equals(code, "invalid_grant", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "InvalidAuthenticationToken", StringComparison.OrdinalIgnoreCase);
}
