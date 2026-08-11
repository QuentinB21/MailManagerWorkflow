using MailManager.Api.Configuration;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Tests;

public sealed class GmailOAuthConfigurationServiceTests
{
    [Fact]
    public async Task Configuration_saved_from_application_encrypts_client_secret()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(options);
        var service = CreateService(dbContext);

        var response = await service.SaveAsync(
            new GmailOAuthConfigurationRequest(
                "client.apps.googleusercontent.com",
                "plain-client-secret"),
            default);

        var stored = Assert.Single(dbContext.GmailOAuthConfigurations);
        Assert.True(response.IsConfigured);
        Assert.Equal("Application", response.Source);
        Assert.NotEqual("plain-client-secret", stored.EncryptedClientSecret);
        Assert.Equal("plain-client-secret", (await service.GetCredentialsAsync(default))?.ClientSecret);
    }

    [Fact]
    public async Task Blank_secret_during_update_preserves_existing_secret()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(options);
        var service = CreateService(dbContext);
        await service.SaveAsync(
            new GmailOAuthConfigurationRequest("first.apps.googleusercontent.com", "secret"),
            default);

        await service.SaveAsync(
            new GmailOAuthConfigurationRequest("second.apps.googleusercontent.com", null),
            default);

        var credentials = await service.GetCredentialsAsync(default);
        Assert.Equal("second.apps.googleusercontent.com", credentials?.ClientId);
        Assert.Equal("secret", credentials?.ClientSecret);
    }

    private static GmailOAuthConfigurationService CreateService(MailManagerDbContext dbContext)
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        return new GmailOAuthConfigurationService(
            dbContext,
            new GmailTokenProtector(dataProtection),
            Options.Create(new GmailOptions()));
    }
}
