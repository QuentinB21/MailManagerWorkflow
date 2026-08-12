using MailManager.Api.Configuration;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Tests;

public sealed class GmailOAuthConfigurationServiceTests
{
    [Fact]
    public async Task Environment_configuration_is_used_without_exposing_credentials()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(options);
        var service = CreateService(dbContext, new GmailOptions
        {
            ClientId = "client.apps.googleusercontent.com",
            ClientSecret = "server-secret"
        });

        var response = await service.GetStatusAsync(default);
        var credentials = await service.GetCredentialsAsync(default);

        Assert.True(response.IsConfigured);
        Assert.Equal("Environment", response.Source);
        Assert.Equal("client.apps.googleusercontent.com", credentials?.ClientId);
        Assert.Equal("server-secret", credentials?.ClientSecret);
    }

    [Fact]
    public async Task Legacy_database_configuration_remains_readable_during_migration()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(options);
        var dataProtection = new EphemeralDataProtectionProvider();
        var protector = new GmailTokenProtector(dataProtection);
        dbContext.GmailOAuthConfigurations.Add(new GmailOAuthConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = "legacy.apps.googleusercontent.com",
            EncryptedClientSecret = protector.ProtectClientSecret("legacy-secret")
        });
        await dbContext.SaveChangesAsync();
        var service = new GmailOAuthConfigurationService(
            dbContext,
            protector,
            Options.Create(new GmailOptions()));

        var response = await service.GetStatusAsync(default);
        var credentials = await service.GetCredentialsAsync(default);

        Assert.True(response.IsConfigured);
        Assert.Equal("LegacyDatabase", response.Source);
        Assert.Equal("legacy.apps.googleusercontent.com", credentials?.ClientId);
        Assert.Equal("legacy-secret", credentials?.ClientSecret);
    }

    private static GmailOAuthConfigurationService CreateService(
        MailManagerDbContext dbContext,
        GmailOptions options)
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        return new GmailOAuthConfigurationService(
            dbContext,
            new GmailTokenProtector(dataProtection),
            Options.Create(options));
    }
}
