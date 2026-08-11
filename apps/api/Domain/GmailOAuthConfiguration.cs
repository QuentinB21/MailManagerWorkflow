namespace MailManager.Api.Domain;

public sealed class GmailOAuthConfiguration
{
    public Guid Id { get; set; }
    public required string ClientId { get; set; }
    public required string EncryptedClientSecret { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
