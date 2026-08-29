using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Services;

public sealed class AccountDataService(
    MailManagerDbContext dbContext,
    CurrentUser currentUser,
    GmailOAuthService gmailOAuthService,
    ILogger<AccountDataService> logger)
{
    public async Task<AccountExport> ExportAsync(CancellationToken cancellationToken)
    {
        var mailboxes = await dbContext.MailboxConnections
            .AsNoTracking()
            .Where(item => item.OwnerSubject == currentUser.Subject)
            .Include(item => item.Labels)
            .Include(item => item.Rules)
            .Include(item => item.ProcessingLogs)
            .AsSplitQuery()
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var acceptance = await dbContext.LegalAcceptances
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerSubject == currentUser.Subject, cancellationToken);

        return new AccountExport(
            DateTimeOffset.UtcNow,
            currentUser.Subject,
            currentUser.DisplayName,
            mailboxes.Select(mailbox => new ExportedMailbox(
                mailbox.Id,
                mailbox.DisplayName,
                mailbox.Provider,
                mailbox.EmailAddress,
                mailbox.IsActive,
                mailbox.CreatedAt,
                mailbox.ConnectedAt,
                mailbox.LastSyncAt,
                mailbox.Labels.OrderBy(item => item.Name).Select(item => new ExportedLabel(
                    item.Id, item.Name, item.Color, item.IsActive)).ToArray(),
                mailbox.Rules.OrderBy(item => item.Priority).Select(item => new ExportedRule(
                    item.Id,
                    item.DestinationLabelId,
                    item.Name,
                    item.Priority,
                    item.IsActive,
                    item.MatchMode,
                    item.SenderAddresses,
                    item.SenderDomains,
                    item.SubjectKeywords,
                    item.BodyKeywords,
                    item.CreatedAt,
                    item.UpdatedAt)).ToArray(),
                mailbox.ProcessingLogs.OrderByDescending(item => item.ProcessedAt).Select(item => new ExportedProcessingLog(
                    item.Id,
                    item.ExternalMessageId,
                    item.SubjectPreview,
                    item.IsClassified,
                    item.DestinationLabelName,
                    item.MatchedRuleName,
                    item.MatchedRulePriority,
                    item.MatchedCriteria,
                    item.NoMatchReason,
                    item.ProviderLabelAppliedAt,
                    item.ProviderActionError,
                    item.ProcessedAt)).ToArray())).ToArray(),
            acceptance is null
                ? null
                : new LegalAcceptanceExport(acceptance.TermsVersion, acceptance.PrivacyVersion, acceptance.AcceptedAt));
    }

    public async Task DeleteApplicationDataAsync(CancellationToken cancellationToken)
    {
        var mailboxes = await dbContext.MailboxConnections
            .Where(item => item.OwnerSubject == currentUser.Subject)
            .ToListAsync(cancellationToken);

        foreach (var mailbox in mailboxes.Where(item =>
                     item.Provider == MailProvider.Gmail
                     && !string.IsNullOrWhiteSpace(item.EncryptedRefreshToken)))
        {
            try
            {
                await gmailOAuthService.DisconnectAsync(mailbox.EncryptedRefreshToken!, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogInformation(exception,
                    "Le jeton Gmail de la boîte {MailboxId} n'a pas pu être révoqué pendant l'effacement du compte.",
                    mailbox.Id);
            }
        }

        dbContext.MailboxConnections.RemoveRange(mailboxes);
        var acceptance = await dbContext.LegalAcceptances
            .SingleOrDefaultAsync(item => item.OwnerSubject == currentUser.Subject, cancellationToken);
        if (acceptance is not null) dbContext.LegalAcceptances.Remove(acceptance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
