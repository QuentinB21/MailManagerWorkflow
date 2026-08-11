using Microsoft.AspNetCore.DataProtection;

namespace MailManager.Api.Services;

public sealed class GmailTokenProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("MailManager.Gmail.RefreshToken.v1");
    private readonly IDataProtector _clientSecretProtector =
        provider.CreateProtector("MailManager.Gmail.ClientSecret.v1");

    public string Protect(string token) => _protector.Protect(token);

    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);

    public string ProtectClientSecret(string clientSecret) =>
        _clientSecretProtector.Protect(clientSecret);

    public string UnprotectClientSecret(string protectedClientSecret) =>
        _clientSecretProtector.Unprotect(protectedClientSecret);
}
