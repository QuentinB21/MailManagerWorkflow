using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailManager.Api.Configuration;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Services;

public sealed class OutlookOAuthService(
    MailManagerDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    OutlookTokenProtector tokenProtector,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<OutlookOptions> options)
{
    private readonly OutlookOptions _options = options.Value;
    private readonly IDataProtector _stateProtector =
        dataProtectionProvider.CreateProtector("MailManager.Outlook.OAuthState.v1");

    public string WebAppUrl => _options.WebAppUrl.TrimEnd('/');
    public bool IsConfigured => _options.IsConfigured;

    public async Task<string?> CreateAuthorizationUrlAsync(Guid mailboxConnectionId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var exists = await dbContext.MailboxConnections.AnyAsync(
            mailbox => mailbox.Id == mailboxConnectionId
                && mailbox.Provider == MailProvider.Outlook
                && mailbox.IsActive,
            cancellationToken);
        if (!exists) return null;

        var state = _stateProtector.Protect(JsonSerializer.Serialize(new OAuthState(
            mailboxConnectionId,
            DateTimeOffset.UtcNow.AddMinutes(10))));
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _options.RedirectUri,
            ["response_mode"] = "query",
            ["scope"] = OutlookOptions.Scopes,
            ["state"] = state,
            ["prompt"] = "select_account"
        };

        return AuthorizationEndpoint + "?" + string.Join("&", query.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
    }

    public async Task<Guid> CompleteAuthorizationAsync(string state, string code, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var statePayload = JsonSerializer.Deserialize<OAuthState>(_stateProtector.Unprotect(state))
            ?? throw new InvalidOperationException("État OAuth Outlook invalide.");
        if (statePayload.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("La demande de connexion Outlook a expiré.");
        }

        var mailbox = await dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == statePayload.MailboxConnectionId
                && item.Provider == MailProvider.Outlook,
            cancellationToken) ?? throw new InvalidOperationException("Boîte Outlook introuvable.");
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = OutlookOptions.Scopes,
            ["grant_type"] = "authorization_code"
        }, cancellationToken);

        var profile = await GetProfileAsync(token.AccessToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new InvalidOperationException("Microsoft n’a pas retourné de jeton durable. Réessayez la connexion.");
        }

        if (!string.IsNullOrWhiteSpace(mailbox.EmailAddress)
            && !string.Equals(mailbox.EmailAddress, profile.EmailAddress, StringComparison.OrdinalIgnoreCase))
        {
            await ResetExternalDestinationsAsync(mailbox.Id, cancellationToken);
        }
        mailbox.EncryptedRefreshToken = tokenProtector.Protect(token.RefreshToken);
        mailbox.EmailAddress = profile.EmailAddress;
        mailbox.DisplayName = profile.EmailAddress;
        mailbox.GrantedScopes = token.Scope ?? OutlookOptions.Scopes;
        mailbox.ConnectedAt = DateTimeOffset.UtcNow;
        mailbox.LastSyncError = null;
        mailbox.RequiresReconnect = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return mailbox.Id;
    }

    public async Task<string> GetAccessTokenAsync(MailboxConnection mailbox, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (mailbox.Provider != MailProvider.Outlook || string.IsNullOrWhiteSpace(mailbox.EncryptedRefreshToken))
        {
            throw new OutlookConfigurationException("Cette boîte Outlook n’est pas connectée.");
        }

        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = tokenProtector.Unprotect(mailbox.EncryptedRefreshToken),
            ["scope"] = OutlookOptions.Scopes,
            ["grant_type"] = "refresh_token"
        }, cancellationToken);

        // Microsoft can rotate refresh tokens. Persist the newest token immediately.
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            mailbox.EncryptedRefreshToken = tokenProtector.Protect(token.RefreshToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return token.AccessToken;
    }

    private string AuthorizationEndpoint =>
        $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.Tenant)}/oauth2/v2.0/authorize";
    private string TokenEndpoint =>
        $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.Tenant)}/oauth2/v2.0/token";

    private async Task<MicrosoftTokenResponse> RequestTokenAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("MicrosoftOAuth");
        using var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(parameters), cancellationToken);
        return await ReadJsonAsync<MicrosoftTokenResponse>(response, cancellationToken);
    }

    private async Task<MicrosoftProfile> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("MicrosoftGraph");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me?$select=mail,userPrincipalName");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        var profile = await ReadJsonAsync<MicrosoftProfilePayload>(response, cancellationToken);
        return new MicrosoftProfile(profile.Mail ?? profile.UserPrincipalName
            ?? throw new OutlookApiException("Microsoft n’a pas retourné l’adresse de la boîte."));
    }

    private async Task ResetExternalDestinationsAsync(Guid mailboxConnectionId, CancellationToken cancellationToken)
    {
        var labels = await dbContext.LabelDefinitions
            .Where(label => label.MailboxConnectionId == mailboxConnectionId)
            .ToListAsync(cancellationToken);
        foreach (var label in labels) label.ExternalLabelId = null;
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new OutlookConfigurationException("La connexion Outlook n’est pas configurée sur le serveur.");
        }
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new OutlookApiException($"Microsoft a refusé la requête ({(int)response.StatusCode}).", details);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new OutlookApiException("Réponse Microsoft vide.");
    }

    private sealed record OAuthState(Guid MailboxConnectionId, DateTimeOffset ExpiresAt);
    private sealed record MicrosoftProfile(string EmailAddress);
    private sealed record MicrosoftProfilePayload(string? Mail, string? UserPrincipalName);
    private sealed record MicrosoftTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);
}

public sealed class OutlookConfigurationException(string message) : InvalidOperationException(message);
public sealed class OutlookApiException(string message, string? providerDetails = null) : Exception(message)
{
    public string? ProviderDetails { get; } = providerDetails;
}
