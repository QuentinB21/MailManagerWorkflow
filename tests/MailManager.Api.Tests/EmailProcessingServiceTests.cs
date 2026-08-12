using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Tests;

public sealed class EmailProcessingServiceTests
{
    [Fact]
    public async Task Processing_same_external_message_twice_is_idempotent()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(options);
        var mailboxId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = mailboxId,
            DisplayName = "Test",
            Provider = "Gmail"
        });
        dbContext.LabelDefinitions.Add(new LabelDefinition
        {
            Id = labelId,
            MailboxConnectionId = mailboxId,
            Name = "Client"
        });
        dbContext.ClassificationRules.Add(new ClassificationRule
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = mailboxId,
            DestinationLabelId = labelId,
            Name = "Domaine client",
            Priority = 7,
            SenderDomains = ["client.fr"]
        });
        await dbContext.SaveChangesAsync();

        var service = new EmailProcessingService(dbContext, new ClassificationEngine());
        var email = new NormalizedEmailRequest(
            mailboxId,
            "gmail-message-42",
            "alice@client.fr",
            "Bonjour",
            "Contenu non persisté");

        var first = await service.ProcessAsync(email);
        var second = await service.ProcessAsync(email);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first.WasAlreadyProcessed);
        Assert.True(second.WasAlreadyProcessed);
        Assert.Equal(first.ProcessingLogId, second.ProcessingLogId);
        Assert.Equal(first.MatchedRule?.Priority, second.MatchedRule?.Priority);
        Assert.Equal(1, await dbContext.ProcessingLogs.CountAsync());
        Assert.DoesNotContain("Contenu non persisté", dbContext.ProcessingLogs.Single().MatchedCriteria);
    }

    [Fact]
    public async Task Processing_stores_a_normalized_subject_preview()
    {
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new MailManagerDbContext(options);
        var mailboxId = Guid.NewGuid();
        dbContext.MailboxConnections.Add(new MailboxConnection
        {
            Id = mailboxId,
            DisplayName = "Test",
            Provider = "Gmail"
        });
        await dbContext.SaveChangesAsync();

        var service = new EmailProcessingService(dbContext, new ClassificationEngine());
        await service.ProcessAsync(new NormalizedEmailRequest(
            mailboxId,
            "gmail-message-subject",
            "alice@example.com",
            "  Compte-rendu\n    projet Atlas  ",
            "Email body must not be stored."));

        Assert.Equal("Compte-rendu projet Atlas", dbContext.ProcessingLogs.Single().SubjectPreview);
    }
}
