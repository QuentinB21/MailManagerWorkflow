namespace MailManager.Api.Domain;

public sealed class LabelDefinition
{
    public Guid Id { get; set; }
    public Guid MailboxConnectionId { get; set; }
    public required string Name { get; set; }
    public string? ExternalLabelId { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MailboxConnection? MailboxConnection { get; set; }
    public ICollection<ClassificationRule> Rules { get; set; } = [];
}
