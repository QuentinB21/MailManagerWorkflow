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
    private readonly GmailOptions _fallbackOptions = options.Value;

    public async Task<GmailOAuthCredentials?> GetCredentialsAsync(
        CancellationToken cancellationToken)
    {
        if (_fallbackOptions.IsConfigured)
        {
            return new GmailOAuthCredentials(
                _fallbackOptions.ClientId,
                _fallbackOptions.ClientSecret);
        }

        // Compatibilité avec les installations locales configurées avant que les
        // identifiants OAuth deviennent exclusivement administrés par le serveur.
        var stored = await dbContext.GmailOAuthConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return stored is null
            ? null
            : new GmailOAuthCredentials(
                stored.ClientId,
                tokenProtector.UnprotectClientSecret(stored.EncryptedClientSecret));
    }

    public async Task<GmailOAuthConfigurationResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        if (_fallbackOptions.IsConfigured)
        {
            return new GmailOAuthConfigurationResponse(true, "Environment");
        }

        var hasLegacyConfiguration = await dbContext.GmailOAuthConfigurations
            .AsNoTracking()
            .AnyAsync(cancellationToken);
        return new GmailOAuthConfigurationResponse(
            hasLegacyConfiguration,
            hasLegacyConfiguration ? "LegacyDatabase" : "None");
    }
}

public sealed record GmailOAuthCredentials(string ClientId, string ClientSecret);
