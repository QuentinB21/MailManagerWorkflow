using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/rules")]
public sealed class RulesController(MailManagerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<RuleResponse>>> GetAll(
        [FromQuery] Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var rules = await dbContext.ClassificationRules
            .AsNoTracking()
            .Include(x => x.DestinationLabel)
            .Where(x => x.MailboxConnectionId == mailboxConnectionId)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(rules.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RuleResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var rule = await dbContext.ClassificationRules
            .AsNoTracking()
            .Include(x => x.DestinationLabel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return rule is null ? NotFound() : Ok(ToResponse(rule));
    }

    [HttpPost]
    public async Task<ActionResult<RuleResponse>> Create(
        RuleRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRequest(request, null, cancellationToken);
        if (validation is not null)
        {
            return BadRequest(new { error = validation });
        }

        var rule = new ClassificationRule
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = request.MailboxConnectionId,
            DestinationLabelId = request.DestinationLabelId,
            Name = request.Name.Trim(),
            Priority = request.Priority,
            IsActive = request.IsActive,
            MatchMode = request.MatchMode
        };
        ApplyCriteria(rule, request);

        dbContext.ClassificationRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        rule.DestinationLabel = await dbContext.LabelDefinitions.AsNoTracking()
            .FirstAsync(x => x.Id == rule.DestinationLabelId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, ToResponse(rule));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RuleResponse>> Update(
        Guid id,
        RuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await dbContext.ClassificationRules
            .Include(x => x.DestinationLabel)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        var validation = await ValidateRequest(request, rule, cancellationToken);
        if (validation is not null)
        {
            return BadRequest(new { error = validation });
        }

        rule.DestinationLabelId = request.DestinationLabelId;
        rule.Name = request.Name.Trim();
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.MatchMode = request.MatchMode;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        ApplyCriteria(rule, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        rule.DestinationLabel = await dbContext.LabelDefinitions.AsNoTracking()
            .FirstAsync(x => x.Id == rule.DestinationLabelId, cancellationToken);
        return Ok(ToResponse(rule));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var rule = await dbContext.ClassificationRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        dbContext.ClassificationRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> ValidateRequest(
        RuleRequest request,
        ClassificationRule? existing,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Le nom de la règle est obligatoire.";
        }

        if (request.Priority < 0)
        {
            return "La priorité doit être positive ou nulle (la plus petite valeur gagne).";
        }

        if (existing is not null && existing.MailboxConnectionId != request.MailboxConnectionId)
        {
            return "Une règle ne peut pas être déplacée vers une autre boîte.";
        }

        var labelExists = await dbContext.LabelDefinitions.AnyAsync(
            x => x.Id == request.DestinationLabelId
                && x.MailboxConnectionId == request.MailboxConnectionId,
            cancellationToken);
        if (!labelExists)
        {
            return "Le label de destination n'appartient pas à cette boîte.";
        }

        var hasCriterion = new[]
        {
            request.SenderAddresses,
            request.SenderDomains,
            request.SubjectKeywords,
            request.BodyKeywords
        }.Any(values => values?.Any(value => !string.IsNullOrWhiteSpace(value)) == true);

        return hasCriterion ? null : "Au moins un critère de correspondance est obligatoire.";
    }

    private static void ApplyCriteria(ClassificationRule rule, RuleRequest request)
    {
        rule.SenderAddresses = RuleValueNormalizer.Values(request.SenderAddresses);
        rule.SenderDomains = RuleValueNormalizer.Values(request.SenderDomains, domain: true);
        rule.SubjectKeywords = RuleValueNormalizer.Values(request.SubjectKeywords);
        rule.BodyKeywords = RuleValueNormalizer.Values(request.BodyKeywords);
    }

    private static RuleResponse ToResponse(ClassificationRule rule) =>
        new(
            rule.Id,
            rule.MailboxConnectionId,
            rule.DestinationLabelId,
            rule.DestinationLabel?.Name ?? "Label inconnu",
            rule.Name,
            rule.Priority,
            rule.IsActive,
            rule.MatchMode,
            rule.SenderAddresses,
            rule.SenderDomains,
            rule.SubjectKeywords,
            rule.BodyKeywords);
}
