namespace MailManager.Api.Domain;

public sealed class LegalAcceptance
{
    public Guid Id { get; set; }
    public required string OwnerSubject { get; set; }
    public required string TermsVersion { get; set; }
    public required string PrivacyVersion { get; set; }
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}
