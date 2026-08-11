using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;

namespace MailManager.Api.Tests;

public sealed class GmailTokenProtectorTests
{
    [Fact]
    public void Refresh_token_is_encrypted_and_can_be_decrypted()
    {
        var protector = new GmailTokenProtector(new EphemeralDataProtectionProvider());

        var encrypted = protector.Protect("refresh-token-secret");

        Assert.NotEqual("refresh-token-secret", encrypted);
        Assert.Equal("refresh-token-secret", protector.Unprotect(encrypted));
    }

    [Fact]
    public void Refresh_token_cannot_be_decrypted_with_another_key_ring()
    {
        var first = new GmailTokenProtector(new EphemeralDataProtectionProvider());
        var second = new GmailTokenProtector(new EphemeralDataProtectionProvider());
        var encrypted = first.Protect("refresh-token-secret");

        Assert.ThrowsAny<Exception>(() => second.Unprotect(encrypted));
    }
}
