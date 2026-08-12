using MailManager.Api.Configuration;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Tests;

public sealed class GmailOAuthServiceTests
{
    [Fact]
    public async Task Authorization_url_uses_offline_access_state_and_minimal_scope()
    {
        var dbOptions = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(dbOptions);
        var mailboxId = Guid.NewGuid();
        dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = mailboxId,
            DisplayName = "Gmail test",
            Provider = MailProvider.Gmail
        });
        await dbContext.SaveChangesAsync();
        var dataProtection = new EphemeralDataProtectionProvider();
        var gmailOptions = Options.Create(new GmailOptions
        {
            ClientId = "client-id.apps.googleusercontent.com",
            ClientSecret = "client-secret",
            RedirectUri = "http://localhost:8080/api/gmail/oauth/callback"
        });
        var tokenProtector = new GmailTokenProtector(dataProtection);
        var configurationService = new GmailOAuthConfigurationService(
            dbContext,
            tokenProtector,
            gmailOptions);
        var service = new GmailOAuthService(
            dbContext,
            new HttpClientFactoryStub(),
            tokenProtector,
            dataProtection,
            configurationService,
            gmailOptions);

        var authorizationUrl = await service.CreateAuthorizationUrlAsync(mailboxId, default);

        Assert.NotNull(authorizationUrl);
        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", authorizationUrl);
        Assert.Contains("access_type=offline", authorizationUrl);
        Assert.Contains("prompt=consent", authorizationUrl);
        Assert.Contains(Uri.EscapeDataString(GmailOptions.ModifyScope), authorizationUrl);
        Assert.Contains("state=", authorizationUrl);
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
