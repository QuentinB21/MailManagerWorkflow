namespace MailManager.Api.Domain;

public enum MatchMode
{
    Any,
    All
}

public sealed class ClassificationRule
{
    public Guid Id { get; set; }
    public Guid MailboxConnectionId { get; set; }
    public Guid DestinationLabelId { get; set; }
    public required string Name { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public MatchMode MatchMode { get; set; } = MatchMode.Any;
    public string[] SenderAddresses { get; set; } = [];
    public string[] SenderDomains { get; set; } = [];
    public string[] SubjectKeywords { get; set; } = [];
    public string[] BodyKeywords { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MailboxConnection? MailboxConnection { get; set; }
    public LabelDefinition? DestinationLabel { get; set; }
}
