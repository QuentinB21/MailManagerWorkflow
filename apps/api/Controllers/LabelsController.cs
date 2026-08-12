using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/labels")]
public sealed class LabelsController(MailManagerDbContext dbContext) : ControllerBase
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
        if (!await MailboxExists(request.MailboxConnectionId, cancellationToken))
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

        var label = new LabelDefinition
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = request.MailboxConnectionId,
            Name = name,
            ExternalLabelId = NullIfEmpty(request.ExternalLabelId),
            Color = NullIfEmpty(request.Color),
            IsActive = request.IsActive
        };

        dbContext.LabelDefinitions.Add(label);
        await dbContext.SaveChangesAsync(cancellationToken);
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

        var nameChanged = !string.Equals(label.Name, name, StringComparison.Ordinal);
        label.Name = name;
        // Provider destination identifiers point to the old name and must be resolved again.
        label.ExternalLabelId = nameChanged ? null : NullIfEmpty(request.ExternalLabelId);
        label.Color = NullIfEmpty(request.Color);
        label.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
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

    private Task<bool> MailboxExists(Guid id, CancellationToken cancellationToken) =>
        dbContext.MailboxConnections.AnyAsync(x => x.Id == id, cancellationToken);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LabelResponse ToResponse(LabelDefinition label) =>
        new(label.Id, label.MailboxConnectionId, label.Name, label.ExternalLabelId, label.Color, label.IsActive);
}
