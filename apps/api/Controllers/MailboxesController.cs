using MailManager.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MailManager.Api.Services;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/mailboxes")]
public sealed class MailboxesController(
    MailManagerDbContext dbContext,
    GmailOAuthConfigurationService gmailConfigurationService) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveAutomationTarget(CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                MailboxConnectionId = item.Id,
                IsConnected = item.EncryptedRefreshToken != null,
                MaxResults = 20
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(mailbox ?? new
        {
            MailboxConnectionId = Guid.Empty,
            IsConnected = false,
            MaxResults = 20
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var gmailConfiguration = await gmailConfigurationService.GetStatusAsync(cancellationToken);
        var mailboxes = await dbContext.MailboxConnections
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.Provider,
                x.IsActive,
                x.EmailAddress,
                IsConnected = x.EncryptedRefreshToken != null,
                OAuthConfigured = gmailConfiguration.IsConfigured,
                x.ConnectedAt,
                x.LastSyncAt,
                x.LastSyncError
            })
            .ToListAsync(cancellationToken);

        return Ok(mailboxes);
    }
}
