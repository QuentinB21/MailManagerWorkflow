using MailManager.Api.Configuration;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Services;

public sealed class GmailOAuthConfigurationService(
    MailManagerDbContext dbContext,
    GmailTokenProtector tokenProtector,
    IOptions<GmailOptions> options)
{
    private static readonly Guid ConfigurationId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly GmailOptions _fallbackOptions = options.Value;

    public async Task<GmailOAuthCredentials?> GetCredentialsAsync(
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.GmailOAuthConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is not null)
        {
            return new GmailOAuthCredentials(
                stored.ClientId,
                tokenProtector.UnprotectClientSecret(stored.EncryptedClientSecret));
        }

        return _fallbackOptions.IsConfigured
            ? new GmailOAuthCredentials(_fallbackOptions.ClientId, _fallbackOptions.ClientSecret)
            : null;
    }

    public async Task<GmailOAuthConfigurationResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.GmailOAuthConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var isEnvironmentConfigured = stored is null && _fallbackOptions.IsConfigured;
        return Response(
            stored is not null || isEnvironmentConfigured,
            stored?.ClientId ?? (isEnvironmentConfigured ? _fallbackOptions.ClientId : null),
            stored is not null || isEnvironmentConfigured,
            stored is not null ? "Application" : isEnvironmentConfigured ? "Environment" : "None");
    }

    public async Task<GmailOAuthConfigurationResponse> SaveAsync(
        GmailOAuthConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = request.ClientId.Trim();
        if (clientId.Length == 0 || !clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new GmailConfigurationException(
                "L’identifiant client Google doit se terminer par .apps.googleusercontent.com.");
        }

        var stored = await dbContext.GmailOAuthConfigurations.FirstOrDefaultAsync(cancellationToken);
        var clientSecret = request.ClientSecret?.Trim();
        if (stored is null && string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new GmailConfigurationException("Le secret client Google est obligatoire lors de la première configuration.");
        }

        if (stored is null)
        {
            stored = new GmailOAuthConfiguration
            {
                Id = ConfigurationId,
                ClientId = clientId,
                EncryptedClientSecret = tokenProtector.ProtectClientSecret(clientSecret!)
            };
            dbContext.GmailOAuthConfigurations.Add(stored);
        }
        else
        {
            stored.ClientId = clientId;
            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                stored.EncryptedClientSecret = tokenProtector.ProtectClientSecret(clientSecret);
            }
            stored.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Response(true, stored.ClientId, true, "Application");
    }

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        var stored = await dbContext.GmailOAuthConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (stored is null) return false;
        dbContext.GmailOAuthConfigurations.Remove(stored);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private GmailOAuthConfigurationResponse Response(
        bool isConfigured,
        string? clientId,
        bool hasClientSecret,
        string source) => new(
            isConfigured,
            clientId,
            hasClientSecret,
            source,
            _fallbackOptions.RedirectUri,
            "https://console.cloud.google.com/apis/library/gmail.googleapis.com",
            "https://console.cloud.google.com/auth/branding",
            "https://console.cloud.google.com/auth/audience",
            "https://console.cloud.google.com/auth/clients");
}

public sealed record GmailOAuthCredentials(string ClientId, string ClientSecret);
