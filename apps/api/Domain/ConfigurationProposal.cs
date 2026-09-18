namespace MailManager.Api.Domain;

public sealed class ConfigurationProposal
{
    public Guid Id { get; set; }
    public Guid MailboxConnectionId { get; set; }
    public required string OwnerSubject { get; set; }
    public required string BeforeJson { get; set; }
    public required string AfterJson { get; set; }
    public required string ChangedDestinationIdsJson { get; set; }
    public required string ChangedRuleIdsJson { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public string SynchronizationStatus { get; set; } = "not_applied";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public MailboxConnection? MailboxConnection { get; set; }
}
