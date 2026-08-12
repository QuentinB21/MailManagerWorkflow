using MailManager.Api.Configuration;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Tests;

public sealed class OutlookOAuthServiceTests
{
    [Fact]
    public async Task Authorization_url_uses_common_tenant_offline_access_and_required_scopes()
    {
        var dbOptions = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MailManagerDbContext(dbOptions);
        var mailboxId = Guid.NewGuid();
        db.MailboxConnections.Add(new MailboxConnection
        {
            Id = mailboxId,
            DisplayName = "Outlook test",
            Provider = MailProvider.Outlook
        });
        await db.SaveChangesAsync();
        var dataProtection = new EphemeralDataProtectionProvider();
        var options = Options.Create(new OutlookOptions
        {
            ClientId = "microsoft-client-id",
            ClientSecret = "client-secret",
            Tenant = "common",
            RedirectUri = "http://localhost:8080/api/outlook/oauth/callback"
        });
        var service = new OutlookOAuthService(
            db,
            new HttpClientFactoryStub(),
            new OutlookTokenProtector(dataProtection),
            dataProtection,
            options);

        var url = await service.CreateAuthorizationUrlAsync(mailboxId, default);

        Assert.NotNull(url);
        Assert.StartsWith("https://login.microsoftonline.com/common/oauth2/v2.0/authorize?", url);
        Assert.Contains(Uri.EscapeDataString(OutlookOptions.Scopes), url);
        Assert.Contains("state=", url);
        Assert.Contains("prompt=select_account", url);
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
