namespace MailManager.Api.Domain;

public sealed class ProcessingLog
{
    public Guid Id { get; set; }
    public Guid MailboxConnectionId { get; set; }
    public required string ExternalMessageId { get; set; }
    public string? SubjectPreview { get; set; }
    public bool IsClassified { get; set; }
    public Guid? DestinationLabelId { get; set; }
    public Guid? MatchedRuleId { get; set; }
    public string? DestinationLabelName { get; set; }
    public string? MatchedRuleName { get; set; }
    public int? MatchedRulePriority { get; set; }
    public string[] MatchedCriteria { get; set; } = [];
    public string? NoMatchReason { get; set; }
    public DateTimeOffset? ProviderLabelAppliedAt { get; set; }
    public string? ProviderActionError { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;

    public MailboxConnection? MailboxConnection { get; set; }
}
