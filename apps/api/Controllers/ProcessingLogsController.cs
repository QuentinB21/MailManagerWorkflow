using MailManager.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/processing-logs")]
public sealed class ProcessingLogsController(MailManagerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid mailboxConnectionId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var logs = await dbContext.ProcessingLogs
            .AsNoTracking()
            .Where(x => x.MailboxConnectionId == mailboxConnectionId)
            .OrderByDescending(x => x.ProcessedAt)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.MailboxConnectionId,
                x.ExternalMessageId,
                x.SubjectPreview,
                x.IsClassified,
                x.DestinationLabelId,
                x.DestinationLabelName,
                x.MatchedRuleId,
                x.MatchedRuleName,
                x.MatchedRulePriority,
                x.MatchedCriteria,
                x.NoMatchReason,
                x.ProviderLabelAppliedAt,
                x.ProviderActionError,
                x.ProcessedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }
}
