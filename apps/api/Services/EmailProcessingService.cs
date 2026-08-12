using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Services;

public sealed class EmailProcessingService(
    MailManagerDbContext dbContext,
    ClassificationEngine classificationEngine)
{
    public async Task<ClassificationResultResponse?> SimulateAsync(
        NormalizedEmailRequest email,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.MailboxConnections.AnyAsync(
                x => x.Id == email.MailboxConnectionId && x.IsActive,
                cancellationToken))
        {
            return null;
        }

        var evaluation = await EvaluateAsync(email, cancellationToken);
        return ToResponse(evaluation);
    }

    public async Task<ClassificationResultResponse?> ProcessAsync(
        NormalizedEmailRequest email,
        CancellationToken cancellationToken = default)
    {
        var externalMessageId = email.ExternalMessageId.Trim();
        var existing = await dbContext.ProcessingLogs.AsNoTracking().FirstOrDefaultAsync(
            x => x.MailboxConnectionId == email.MailboxConnectionId
                && x.ExternalMessageId == externalMessageId,
            cancellationToken);

        if (existing is not null)
        {
            return FromLog(existing, true);
        }

        if (!await dbContext.MailboxConnections.AnyAsync(
                x => x.Id == email.MailboxConnectionId && x.IsActive,
                cancellationToken))
        {
            return null;
        }

        var evaluation = await EvaluateAsync(email, cancellationToken);
        var log = new ProcessingLog
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = email.MailboxConnectionId,
            ExternalMessageId = externalMessageId,
            SubjectPreview = CreateSubjectPreview(email.Subject),
            IsClassified = evaluation.IsClassified,
            DestinationLabelId = evaluation.Label?.Id,
            MatchedRuleId = evaluation.Rule?.Id,
            DestinationLabelName = evaluation.Label?.Name,
            MatchedRuleName = evaluation.Rule?.Name,
            MatchedRulePriority = evaluation.Rule?.Priority,
            MatchedCriteria = evaluation.MatchedCriteria.ToArray(),
            NoMatchReason = evaluation.NoMatchReason
        };

        dbContext.ProcessingLogs.Add(log);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery may have inserted the same mailbox/message pair.
            dbContext.Entry(log).State = EntityState.Detached;
            var concurrentLog = await dbContext.ProcessingLogs.AsNoTracking().FirstOrDefaultAsync(
                x => x.MailboxConnectionId == email.MailboxConnectionId
                    && x.ExternalMessageId == externalMessageId,
                cancellationToken);
            if (concurrentLog is not null)
            {
                return FromLog(concurrentLog, true);
            }

            throw;
        }
        return ToResponse(evaluation, processingLogId: log.Id);
    }

    internal static string? CreateSubjectPreview(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return null;

        var normalized = string.Join(' ', subject.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 250 ? normalized : normalized[..247] + "...";
    }

    private async Task<ClassificationEvaluation> EvaluateAsync(
        NormalizedEmailRequest email,
        CancellationToken cancellationToken)
    {
        var rules = await dbContext.ClassificationRules
            .AsNoTracking()
            .Include(x => x.DestinationLabel)
            .Where(x => x.MailboxConnectionId == email.MailboxConnectionId)
            .ToListAsync(cancellationToken);

        return classificationEngine.Evaluate(email, rules);
    }

    private static ClassificationResultResponse ToResponse(
        ClassificationEvaluation evaluation,
        bool alreadyProcessed = false,
        Guid? processingLogId = null) =>
        new(
            evaluation.IsClassified,
            evaluation.Label is null ? null : new LabelSummary(evaluation.Label.Id, evaluation.Label.Name),
            evaluation.Rule is null
                ? null
                : new RuleSummary(evaluation.Rule.Id, evaluation.Rule.Name, evaluation.Rule.Priority),
            evaluation.MatchedCriteria,
            evaluation.NoMatchReason,
            alreadyProcessed,
            processingLogId);

    private static ClassificationResultResponse FromLog(ProcessingLog log, bool alreadyProcessed) =>
        new(
            log.IsClassified,
            log.DestinationLabelId is null
                ? null
                : new LabelSummary(log.DestinationLabelId.Value, log.DestinationLabelName ?? "Label supprimé"),
            log.MatchedRuleId is null
                ? null
                : new RuleSummary(
                    log.MatchedRuleId.Value,
                    log.MatchedRuleName ?? "Règle supprimée",
                    log.MatchedRulePriority ?? 0),
            log.MatchedCriteria,
            log.NoMatchReason,
            alreadyProcessed,
            log.Id);
}
