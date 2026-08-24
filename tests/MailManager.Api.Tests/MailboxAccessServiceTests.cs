using System.Security.Claims;
using System.Text.Json;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Tests;

public sealed class MailboxAccessServiceTests
{
    [Fact]
    public async Task Authenticated_user_only_sees_owned_mailboxes()
    {
        await using var db = CreateDbContext();
        db.MailboxConnections.AddRange(
            Mailbox("owner-a"),
            Mailbox("owner-b"));
        await db.SaveChangesAsync();

        var access = new MailboxAccessService(db, CurrentUser("owner-a"));

        var visible = await access.OwnedMailboxes().ToListAsync();

        Assert.Single(visible);
        Assert.Equal("owner-a", visible[0].OwnerSubject);
    }

    [Fact]
    public async Task Automation_role_can_see_all_mailboxes()
    {
        await using var db = CreateDbContext();
        db.MailboxConnections.AddRange(
            Mailbox("owner-a"),
            Mailbox("owner-b"));
        await db.SaveChangesAsync();

        var access = new MailboxAccessService(db, CurrentUser("n8n", "automation"));

        Assert.Equal(2, await access.OwnedMailboxes().CountAsync());
    }

    [Fact]
    public void Demo_role_is_read_from_keycloak_realm_access_claim()
    {
        var currentUser = CurrentUser("demo-subject", "demo");

        Assert.True(currentUser.IsDemo);
        Assert.False(currentUser.IsAutomation);
    }

    private static MailManagerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MailboxConnection Mailbox(string owner) => new()
    {
        Id = Guid.NewGuid(),
        OwnerSubject = owner,
        DisplayName = owner,
        Provider = MailProvider.Gmail
    };

    private static CurrentUser CurrentUser(string subject, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", subject) };
        if (roles.Length > 0)
        {
            claims.Add(new Claim("realm_access", JsonSerializer.Serialize(new { roles })));
        }

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        return new CurrentUser(new HttpContextAccessor { HttpContext = context });
    }
}
