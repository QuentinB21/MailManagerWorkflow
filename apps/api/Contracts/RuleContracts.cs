using System.ComponentModel.DataAnnotations;
using MailManager.Api.Domain;

namespace MailManager.Api.Contracts;

public sealed record RuleRequest(
    [Required] Guid MailboxConnectionId,
    [Required] Guid DestinationLabelId,
    [Required, MaxLength(200)] string Name,
    int Priority,
    bool IsActive,
    MatchMode MatchMode,
    IReadOnlyCollection<string>? SenderAddresses,
    IReadOnlyCollection<string>? SenderDomains,
    IReadOnlyCollection<string>? SubjectKeywords,
    IReadOnlyCollection<string>? BodyKeywords);

public sealed record RuleResponse(
    Guid Id,
    Guid MailboxConnectionId,
    Guid DestinationLabelId,
    string DestinationLabelName,
    string Name,
    int Priority,
    bool IsActive,
    MatchMode MatchMode,
    IReadOnlyCollection<string> SenderAddresses,
    IReadOnlyCollection<string> SenderDomains,
    IReadOnlyCollection<string> SubjectKeywords,
    IReadOnlyCollection<string> BodyKeywords);
