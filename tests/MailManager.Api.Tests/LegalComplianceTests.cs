using System.Security.Claims;
using System.Text.Json;
using MailManager.Api.Configuration;
using MailManager.Api.Contracts;
using MailManager.Api.Controllers;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Security;
using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Tests;

public sealed class LegalComplianceTests
{
    [Fact]
    public async Task Export_only_contains_owned_data_and_never_contains_oauth_token()
    {
        await using var db = CreateDbContext();
        db.MailboxConnections.AddRange(
            Mailbox("owner-a", "encrypted-secret-token"),
            Mailbox("owner-b", "other-owner-token"));
        await db.SaveChangesAsync();
        var service = CreateAccountDataService(db, CurrentUser("owner-a"));

        var export = await service.ExportAsync(CancellationToken.None);
        var json = JsonSerializer.Serialize(export);

        Assert.Single(export.Mailboxes);
        Assert.Equal("owner-a", export.AccountSubject);
        Assert.DoesNotContain("encrypted-secret-token", json);
        Assert.DoesNotContain("other-owner-token", json);
    }

    [Fact]
    public async Task Legal_acceptance_is_versioned_for_current_user()
    {
        await using var db = CreateDbContext();
        var currentUser = CurrentUser("owner-a");
        var controller = new AccountController(db, currentUser, CreateAccountDataService(db, currentUser));

        var result = await controller.AcceptLegalDocuments(
            new AcceptLegalDocumentsRequest(true, true), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        var acceptance = await db.LegalAcceptances.SingleAsync();
        Assert.Equal("owner-a", acceptance.OwnerSubject);
        Assert.Equal(LegalDocumentVersions.Terms, acceptance.TermsVersion);
        Assert.Equal(LegalDocumentVersions.Privacy, acceptance.PrivacyVersion);
    }

    [Fact]
    public async Task Erasure_removes_only_current_users_application_data()
    {
        await using var db = CreateDbContext();
        var owned = Mailbox("owner-a");
        var other = Mailbox("owner-b");
        db.MailboxConnections.AddRange(owned, other);
        db.LegalAcceptances.Add(new LegalAcceptance
        {
            Id = Guid.NewGuid(), OwnerSubject = "owner-a", TermsVersion = "v1", PrivacyVersion = "v1"
        });
        await db.SaveChangesAsync();
        var service = CreateAccountDataService(db, CurrentUser("owner-a"));

        await service.DeleteApplicationDataAsync(CancellationToken.None);

        Assert.False(await db.MailboxConnections.AnyAsync(item => item.OwnerSubject == "owner-a"));
        Assert.True(await db.MailboxConnections.AnyAsync(item => item.OwnerSubject == "owner-b"));
        Assert.Empty(await db.LegalAcceptances.ToListAsync());
    }

    private static MailManagerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MailboxConnection Mailbox(string owner, string? encryptedToken = null) => new()
    {
        Id = Guid.NewGuid(),
        OwnerSubject = owner,
        DisplayName = owner,
        Provider = MailProvider.Outlook,
        EncryptedRefreshToken = encryptedToken
    };

    private static CurrentUser CurrentUser(string subject)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", subject),
                new Claim("name", subject)
            ], "Test"))
        };
        return new CurrentUser(new HttpContextAccessor { HttpContext = context });
    }

    private static AccountDataService CreateAccountDataService(MailManagerDbContext db, CurrentUser currentUser)
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        var tokenProtector = new GmailTokenProtector(dataProtection);
        var options = Options.Create(new GmailOptions());
        var configuration = new GmailOAuthConfigurationService(db, tokenProtector, options);
        var oauth = new GmailOAuthService(
            db,
            new TestHttpClientFactory(),
            tokenProtector,
            dataProtection,
            configuration,
            options);
        return new AccountDataService(db, currentUser, oauth, NullLogger<AccountDataService>.Instance);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
