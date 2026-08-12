using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Tests;

public sealed class MailboxIsolationTests
{
    [Fact]
    public async Task Rules_from_another_mailbox_are_never_evaluated()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MailManagerDbContext(options);
        var gmailId = Guid.NewGuid();
        var outlookId = Guid.NewGuid();
        var gmailLabelId = Guid.NewGuid();
        var outlookLabelId = Guid.NewGuid();
        db.MailboxConnections.AddRange(
            new MailboxConnection { Id = gmailId, DisplayName = "Gmail", Provider = MailProvider.Gmail },
            new MailboxConnection { Id = outlookId, DisplayName = "Outlook", Provider = MailProvider.Outlook });
        db.LabelDefinitions.AddRange(
            new LabelDefinition { Id = gmailLabelId, MailboxConnectionId = gmailId, Name = "Gmail client" },
            new LabelDefinition { Id = outlookLabelId, MailboxConnectionId = outlookId, Name = "Outlook client" });
        db.ClassificationRules.AddRange(
            new ClassificationRule { Id = Guid.NewGuid(), MailboxConnectionId = gmailId, DestinationLabelId = gmailLabelId, Name = "Gmail", SenderDomains = ["client.fr"] },
            new ClassificationRule { Id = Guid.NewGuid(), MailboxConnectionId = outlookId, DestinationLabelId = outlookLabelId, Name = "Outlook", SenderDomains = ["client.fr"] });
        await db.SaveChangesAsync();

        var service = new EmailProcessingService(db, new ClassificationEngine());
        var result = await service.SimulateAsync(new NormalizedEmailRequest(
            outlookId, "message-1", "alice@client.fr", "Sujet", "Corps"));

        Assert.Equal("Outlook client", result?.Label?.Name);
        Assert.Equal("Outlook", result?.MatchedRule?.Name);
    }
}
