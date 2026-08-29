using MailManager.Api.Domain;

namespace MailManager.Api.Contracts;

public sealed record LegalStatusResponse(
    bool IsAccepted,
    string TermsVersion,
    string PrivacyVersion,
    DateTimeOffset? AcceptedAt);

public sealed record AcceptLegalDocumentsRequest(bool AcceptTerms, bool AcknowledgePrivacy);

public sealed record AccountExport(
    DateTimeOffset ExportedAt,
    string AccountSubject,
    string DisplayName,
    IReadOnlyCollection<ExportedMailbox> Mailboxes,
    LegalAcceptanceExport? LegalAcceptance);

public sealed record ExportedMailbox(
    Guid Id,
    string DisplayName,
    MailProvider Provider,
    string? EmailAddress,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? LastSyncAt,
    IReadOnlyCollection<ExportedLabel> Destinations,
    IReadOnlyCollection<ExportedRule> Rules,
    IReadOnlyCollection<ExportedProcessingLog> ProcessingHistory);

public sealed record ExportedLabel(Guid Id, string Name, string? Color, bool IsActive);

public sealed record ExportedRule(
    Guid Id,
    Guid DestinationLabelId,
    string Name,
    int Priority,
    bool IsActive,
    MatchMode MatchMode,
    string[] SenderAddresses,
    string[] SenderDomains,
    string[] SubjectKeywords,
    string[] BodyKeywords,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ExportedProcessingLog(
    Guid Id,
    string ExternalMessageId,
    string? SubjectPreview,
    bool IsClassified,
    string? DestinationLabelName,
    string? MatchedRuleName,
    int? MatchedRulePriority,
    string[] MatchedCriteria,
    string? NoMatchReason,
    DateTimeOffset? ProviderLabelAppliedAt,
    string? ProviderActionError,
    DateTimeOffset ProcessedAt);

public sealed record LegalAcceptanceExport(
    string TermsVersion,
    string PrivacyVersion,
    DateTimeOffset AcceptedAt);
