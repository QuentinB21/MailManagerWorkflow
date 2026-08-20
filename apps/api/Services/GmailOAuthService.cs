using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MailManager.Api.Configuration;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Services;

public sealed class GmailOAuthService(
    MailManagerDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    GmailTokenProtector tokenProtector,
    IDataProtectionProvider dataProtectionProvider,
    GmailOAuthConfigurationService configurationService,
    IOptions<GmailOptions> options)
{
    private readonly GmailOptions _options = options.Value;
    private readonly IDataProtector _stateProtector =
        dataProtectionProvider.CreateProtector("MailManager.Gmail.OAuthState.v1");

    public string RedirectUri => _options.RedirectUri;
    public string WebAppUrl => _options.WebAppUrl.TrimEnd('/');

    public async Task<string?> CreateAuthorizationUrlAsync(
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var credentials = await GetCredentialsAsync(cancellationToken);
        var exists = await dbContext.MailboxConnections.AnyAsync(
            mailbox => mailbox.Id == mailboxConnectionId
                && mailbox.Provider == MailProvider.Gmail
                && mailbox.IsActive,
            cancellationToken);
        if (!exists) return null;

        var statePayload = JsonSerializer.Serialize(new OAuthState(
            mailboxConnectionId,
            DateTimeOffset.UtcNow.AddMinutes(10)));
        var state = _stateProtector.Protect(statePayload);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = GmailOptions.ModifyScope,
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "consent",
            ["state"] = state
        };

        return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join(
            "&",
            query.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
    }

    public async Task<Guid> CompleteAuthorizationAsync(
        string state,
        string code,
        CancellationToken cancellationToken)
    {
        var credentials = await GetCredentialsAsync(cancellationToken);
        var statePayload = JsonSerializer.Deserialize<OAuthState>(_stateProtector.Unprotect(state))
            ?? throw new InvalidOperationException("État OAuth Gmail invalide.");
        if (statePayload.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("La demande de connexion Gmail a expiré.");
        }

        var mailbox = await dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == statePayload.MailboxConnectionId
                && item.Provider == MailProvider.Gmail,
            cancellationToken) ?? throw new InvalidOperationException("Boîte mail introuvable.");

        var client = httpClientFactory.CreateClient("GoogleOAuth");
        using var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code"
            }),
            cancellationToken);
        var tokenPayload = await ReadJsonAsync<GoogleTokenResponse>(tokenResponse, cancellationToken);

        using var profileRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://gmail.googleapis.com/gmail/v1/users/me/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenPayload.AccessToken);
        using var profileResponse = await client.SendAsync(profileRequest, cancellationToken);
        var profile = await ReadJsonAsync<GmailProfile>(profileResponse, cancellationToken);

        if (!string.IsNullOrWhiteSpace(tokenPayload.RefreshToken))
        {
            mailbox.EncryptedRefreshToken = tokenProtector.Protect(tokenPayload.RefreshToken);
        }
        if (string.IsNullOrWhiteSpace(mailbox.EncryptedRefreshToken))
        {
            throw new InvalidOperationException("Google n’a pas retourné de jeton durable. Réessayez la connexion.");
        }

        if (!string.IsNullOrWhiteSpace(mailbox.EmailAddress)
            && !string.Equals(mailbox.EmailAddress, profile.EmailAddress, StringComparison.OrdinalIgnoreCase))
        {
            var labels = await dbContext.LabelDefinitions
                .Where(label => label.MailboxConnectionId == mailbox.Id)
                .ToListAsync(cancellationToken);
            foreach (var label in labels) label.ExternalLabelId = null;
        }
        mailbox.EmailAddress = profile.EmailAddress;
        mailbox.DisplayName = profile.EmailAddress;
        mailbox.GrantedScopes = tokenPayload.Scope ?? GmailOptions.ModifyScope;
        mailbox.ConnectedAt = DateTimeOffset.UtcNow;
        mailbox.LastSyncError = null;
        mailbox.RequiresReconnect = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return mailbox.Id;
    }

    public async Task<string> GetAccessTokenAsync(
        string encryptedRefreshToken,
        CancellationToken cancellationToken)
    {
        var credentials = await GetCredentialsAsync(cancellationToken);
        var refreshToken = tokenProtector.Unprotect(encryptedRefreshToken);
        var client = httpClientFactory.CreateClient("GoogleOAuth");
        using var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }),
            cancellationToken);
        var payload = await ReadJsonAsync<GoogleTokenResponse>(response, cancellationToken);
        return payload.AccessToken;
    }

    public async Task DisconnectAsync(string encryptedRefreshToken, CancellationToken cancellationToken)
    {
        var token = tokenProtector.Unprotect(encryptedRefreshToken);
        var client = httpClientFactory.CreateClient("GoogleOAuth");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
        using var response = await client.PostAsync("https://oauth2.googleapis.com/revoke", content, cancellationToken);
        // Local disconnection must remain possible even if Google already revoked the token.
    }

    private async Task<GmailOAuthCredentials> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        return await configurationService.GetCredentialsAsync(cancellationToken)
            ?? throw new GmailConfigurationException(
                "La connexion Gmail n’est pas configurée sur le serveur.");
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new GmailApiException(
                $"Google a refusé la requête ({(int)response.StatusCode}).",
                details);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new GmailApiException("Réponse Google vide.");
    }

    private sealed record OAuthState(Guid MailboxConnectionId, DateTimeOffset ExpiresAt);
    private sealed record GmailProfile(string EmailAddress);
    private sealed record GoogleTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope);
}

public sealed class GmailConfigurationException(string message) : InvalidOperationException(message);

public sealed class GmailApiException(string message, string? providerDetails = null) : Exception(message)
{
    public string? ProviderDetails { get; } = providerDetails;
}
