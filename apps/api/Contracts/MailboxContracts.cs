using System.ComponentModel.DataAnnotations;
using MailManager.Api.Domain;

namespace MailManager.Api.Contracts;

public sealed record CreateMailboxRequest(MailProvider Provider);
public sealed record MailboxSyncRequest([Range(1, 20)] int MaxResults = 5);

public sealed record MailboxMessageProcessingResult(
    string ExternalMessageId,
    string? Subject,
    bool IsClassified,
    LabelSummary? Label,
    RuleSummary? MatchedRule,
    IReadOnlyCollection<string> MatchedCriteria,
    string? NoMatchReason,
    bool WasAlreadyProcessed,
    bool DestinationApplied,
    string? Error = null);

public sealed record MailboxSyncResponse(
    int RequestedCount,
    int DiscoveredCount,
    int ProcessedCount,
    int ClassifiedCount,
    int DestinationAppliedCount,
    int UnclassifiedCount,
    int FailureCount,
    IReadOnlyCollection<MailboxMessageProcessingResult> Results);

public sealed record ProviderConfigurationResponse(bool IsConfigured, string Source = "Environment");
public sealed record MailboxConnectionTestResponse(bool IsConnected, string EmailAddress);
