using System.ComponentModel.DataAnnotations;

namespace MailManager.Api.Contracts;

public sealed record GmailSyncRequest([Range(1, 20)] int MaxResults = 5);

public sealed record GmailMessageProcessingResult(
    string ExternalMessageId,
    bool IsClassified,
    LabelSummary? Label,
    RuleSummary? MatchedRule,
    IReadOnlyCollection<string> MatchedCriteria,
    string? NoMatchReason,
    bool WasAlreadyProcessed,
    bool LabelApplied,
    string? Error = null);

public sealed record GmailSyncResponse(
    int RequestedCount,
    int DiscoveredCount,
    int ProcessedCount,
    int ClassifiedCount,
    int LabelAppliedCount,
    int UnclassifiedCount,
    int FailureCount,
    IReadOnlyCollection<GmailMessageProcessingResult> Results);

public sealed record GmailConnectionTestResponse(bool IsConnected, string EmailAddress);

public sealed record GmailOAuthConfigurationResponse(
    bool IsConfigured,
    string Source);
