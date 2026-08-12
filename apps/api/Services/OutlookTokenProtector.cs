using Microsoft.AspNetCore.DataProtection;

namespace MailManager.Api.Services;

public sealed class OutlookTokenProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("MailManager.Outlook.RefreshToken.v1");

    public string Protect(string token) => _protector.Protect(token);
    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}
