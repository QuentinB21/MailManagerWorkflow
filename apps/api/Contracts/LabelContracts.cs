using System.ComponentModel.DataAnnotations;

namespace MailManager.Api.Contracts;

public sealed record LabelRequest(
    [Required] Guid MailboxConnectionId,
    [Required, MaxLength(150)] string Name,
    [MaxLength(200)] string? ExternalLabelId,
    [MaxLength(20)] string? Color,
    bool IsActive = true);

public sealed record LabelResponse(
    Guid Id,
    Guid MailboxConnectionId,
    string Name,
    string? ExternalLabelId,
    string? Color,
    bool IsActive);
