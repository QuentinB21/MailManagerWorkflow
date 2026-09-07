using System.Security.Claims;
using MailManager.Api.Configuration;
using MailManager.Api.Controllers;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Security;
using MailManager.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Tests;

public sealed class MailboxesControllerTests
{
    [Fact]
    public async Task Delete_allows_removing_last_disconnected_mailbox()
    {
        await using var db = CreateDbContext();
        var mailbox = Mailbox("owner-a");
        db.MailboxConnections.Add(mailbox);
        await db.SaveChangesAsync();
        var controller = CreateController(db, CurrentUser("owner-a"));

        var result = await controller.Delete(mailbox.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.MailboxConnections.ToListAsync());
    }

    [Fact]
    public async Task Delete_still_rejects_a_connected_mailbox()
    {
        await using var db = CreateDbContext();
        var mailbox = Mailbox("owner-a", "encrypted-refresh-token");
        db.MailboxConnections.Add(mailbox);
        await db.SaveChangesAsync();
        var controller = CreateController(db, CurrentUser("owner-a"));

        var result = await controller.Delete(mailbox.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await db.MailboxConnections.ToListAsync());
    }

    private static MailboxesController CreateController(
        MailManagerDbContext db,
        CurrentUser currentUser)
    {
        var tokenProtector = new GmailTokenProtector(new EphemeralDataProtectionProvider());
        var gmailConfiguration = new GmailOAuthConfigurationService(
            db,
            tokenProtector,
            Options.Create(new GmailOptions()));

        return new MailboxesController(
            db,
            gmailConfiguration,
            Options.Create(new OutlookOptions()),
            new MailboxProviderResolver([]),
            new MailboxAccessService(db, currentUser),
            currentUser);
    }

    private static MailManagerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MailboxConnection Mailbox(string owner, string? encryptedToken = null) => new()
    {
        Id = Guid.NewGuid(),
        OwnerSubject = owner,
        DisplayName = "Gmail",
        Provider = MailProvider.Gmail,
        EncryptedRefreshToken = encryptedToken
    };

    private static CurrentUser CurrentUser(string subject)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", subject)
            ], "Test"))
        };
        return new CurrentUser(new HttpContextAccessor { HttpContext = context });
    }
}
