namespace MailManager.Api.Domain;

public sealed class MailboxConnection
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public MailProvider Provider { get; set; }
    public bool IsActive { get; set; } = true;
    public string? EmailAddress { get; set; }
    public string? EncryptedRefreshToken { get; set; }
    public string? GrantedScopes { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public string? LastSyncError { get; set; }
    public bool RequiresReconnect { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<LabelDefinition> Labels { get; set; } = [];
    public ICollection<ClassificationRule> Rules { get; set; } = [];
    public ICollection<ProcessingLog> ProcessingLogs { get; set; } = [];
}
