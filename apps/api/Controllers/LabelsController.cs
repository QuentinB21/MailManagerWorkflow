using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/labels")]
public sealed class LabelsController(
    MailManagerDbContext dbContext,
    MailboxProviderResolver providerResolver,
    ILogger<LabelsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LabelResponse>>> GetAll(
        [FromQuery] Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var labels = await dbContext.LabelDefinitions
            .AsNoTracking()
            .Where(x => x.MailboxConnectionId == mailboxConnectionId)
            .OrderBy(x => x.Name)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(labels);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LabelResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var label = await dbContext.LabelDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return label is null ? NotFound() : Ok(ToResponse(label));
    }

    [HttpPost]
    public async Task<ActionResult<LabelResponse>> Create(
        LabelRequest request,
        CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == request.MailboxConnectionId,
            cancellationToken);
        if (mailbox is null)
        {
            return BadRequest(new { error = "MailboxConnectionId inconnu." });
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return BadRequest(new { error = "Le nom du label est obligatoire." });
        }

        if (await dbContext.LabelDefinitions.AnyAsync(
                x => x.MailboxConnectionId == request.MailboxConnectionId && x.Name == name,
                cancellationToken))
        {
            return Conflict(new { error = "Un label de ce nom existe déjà pour cette boîte." });
        }

        var requestedColor = NullIfEmpty(request.Color);
        var color = ProviderColorMapper.NormalizeHexColor(requestedColor);
        if (requestedColor is not null && color is null)
        {
            return BadRequest(new { error = "La couleur doit être au format hexadécimal #RRGGBB." });
        }

        var label = new LabelDefinition
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = request.MailboxConnectionId,
            Name = name,
            ExternalLabelId = NullIfEmpty(request.ExternalLabelId),
            Color = color,
            IsActive = request.IsActive
        };

        dbContext.LabelDefinitions.Add(label);
        await dbContext.SaveChangesAsync(cancellationToken);
        var synchronizationError = await SynchronizeIfConnectedAsync(mailbox, label, cancellationToken);
        if (synchronizationError is not null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = synchronizationError });
        }
        return CreatedAtAction(nameof(GetById), new { id = label.Id }, ToResponse(label));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LabelResponse>> Update(
        Guid id,
        LabelRequest request,
        CancellationToken cancellationToken)
    {
        var label = await dbContext.LabelDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (label is null)
        {
            return NotFound();
        }

        if (label.MailboxConnectionId != request.MailboxConnectionId)
        {
            return BadRequest(new { error = "Un label ne peut pas être déplacé vers une autre boîte." });
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return BadRequest(new { error = "Le nom du label est obligatoire." });
        }

        if (await dbContext.LabelDefinitions.AnyAsync(
                x => x.Id != id && x.MailboxConnectionId == request.MailboxConnectionId && x.Name == name,
                cancellationToken))
        {
            return Conflict(new { error = "Un label de ce nom existe déjà pour cette boîte." });
        }

        var requestedColor = NullIfEmpty(request.Color);
        var color = ProviderColorMapper.NormalizeHexColor(requestedColor);
        if (requestedColor is not null && color is null)
        {
            return BadRequest(new { error = "La couleur doit être au format hexadécimal #RRGGBB." });
        }

        var nameChanged = !string.Equals(label.Name, name, StringComparison.Ordinal);
        label.Name = name;
        // Provider destination identifiers point to the old name and must be resolved again.
        label.ExternalLabelId = nameChanged ? null : NullIfEmpty(request.ExternalLabelId);
        label.Color = color;
        label.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        var mailbox = await dbContext.MailboxConnections.FirstAsync(
            item => item.Id == label.MailboxConnectionId,
            cancellationToken);
        var synchronizationError = await SynchronizeIfConnectedAsync(mailbox, label, cancellationToken);
        if (synchronizationError is not null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = synchronizationError });
        }
        return Ok(ToResponse(label));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var label = await dbContext.LabelDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (label is null)
        {
            return NotFound();
        }

        if (await dbContext.ClassificationRules.AnyAsync(x => x.DestinationLabelId == id, cancellationToken))
        {
            return Conflict(new { error = "Ce label est encore utilisé par une règle." });
        }

        dbContext.LabelDefinitions.Remove(label);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> SynchronizeIfConnectedAsync(
        MailboxConnection mailbox,
        LabelDefinition label,
        CancellationToken cancellationToken)
    {
        if (!label.IsActive || string.IsNullOrWhiteSpace(mailbox.EncryptedRefreshToken)) return null;

        try
        {
            await providerResolver.Resolve(mailbox.Provider)
                .SynchronizeDestinationAsync(label.Id, cancellationToken);
            mailbox.LastSyncError = null;
            mailbox.RequiresReconnect = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Impossible de synchroniser la destination {LabelId} avec {Provider}.",
                label.Id,
                mailbox.Provider);
            mailbox.RequiresReconnect = ProviderAuthenticationFailureDetector.RequiresReconnect(exception);
            mailbox.LastSyncError = mailbox.RequiresReconnect
                ? ProviderAuthenticationFailureDetector.ReconnectMessage(mailbox.Provider.ToString())
                : $"La destination est enregistrée, mais sa synchronisation avec {mailbox.Provider} a échoué.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return mailbox.LastSyncError;
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LabelResponse ToResponse(LabelDefinition label) =>
        new(label.Id, label.MailboxConnectionId, label.Name, label.ExternalLabelId, label.Color, label.IsActive);
}
