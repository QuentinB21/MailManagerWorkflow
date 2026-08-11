using System.ComponentModel.DataAnnotations;

namespace MailManager.Api.Contracts;

public sealed record NormalizedEmailRequest(
    [Required] Guid MailboxConnectionId,
    [Required, MaxLength(300)] string ExternalMessageId,
    [Required, EmailAddress, MaxLength(320)] string Sender,
    [MaxLength(1000)] string? Subject,
    string? Body);

public sealed record ClassificationResultResponse(
    bool IsClassified,
    LabelSummary? Label,
    RuleSummary? MatchedRule,
    IReadOnlyCollection<string> MatchedCriteria,
    string? NoMatchReason,
    bool WasAlreadyProcessed = false,
    Guid? ProcessingLogId = null);

public sealed record LabelSummary(Guid Id, string Name);
public sealed record RuleSummary(Guid Id, string Name, int Priority);
