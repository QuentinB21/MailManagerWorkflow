using MailManager.Api.Configuration;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/mailboxes")]
public sealed class MailboxesController(
    MailManagerDbContext dbContext,
    GmailOAuthConfigurationService gmailConfigurationService,
    IOptions<OutlookOptions> outlookOptions,
    MailboxProviderResolver providerResolver) : ControllerBase
{
    [HttpGet("automation-targets")]
    public async Task<IActionResult> GetAutomationTargets(CancellationToken cancellationToken)
    {
        var mailboxes = await dbContext.MailboxConnections
            .AsNoTracking()
            .Where(item => item.IsActive && item.EncryptedRefreshToken != null)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                MailboxConnectionId = item.Id,
                item.Provider,
                IsConnected = true,
                MaxResults = 20
            })
            .ToListAsync(cancellationToken);
        return Ok(mailboxes);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var gmailConfiguration = await gmailConfigurationService.GetStatusAsync(cancellationToken);
        var outlookConfigured = outlookOptions.Value.IsConfigured;
        var mailboxes = await dbContext.MailboxConnections
            .AsNoTracking()
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.Provider,
                x.IsActive,
                x.EmailAddress,
                IsConnected = x.EncryptedRefreshToken != null,
                OAuthConfigured = x.Provider == MailProvider.Gmail
                    ? gmailConfiguration.IsConfigured
                    : outlookConfigured,
                x.ConnectedAt,
                x.LastSyncAt,
                x.LastSyncError
            })
            .ToListAsync(cancellationToken);
        return Ok(mailboxes);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMailboxRequest request, CancellationToken cancellationToken)
    {
        var mailbox = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            Provider = request.Provider,
            DisplayName = request.Provider == MailProvider.Gmail ? "Nouveau compte Gmail" : "Nouveau compte Outlook",
            IsActive = true
        };
        dbContext.MailboxConnections.Add(mailbox);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = mailbox.Id }, new
        {
            mailbox.Id,
            mailbox.DisplayName,
            mailbox.Provider,
            mailbox.IsActive,
            IsConnected = false
        });
    }

    [HttpPost("{mailboxConnectionId:guid}/sync")]
    public async Task<ActionResult<MailboxSyncResponse>> Sync(
        Guid mailboxConnectionId,
        MailboxSyncRequest request,
        CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == mailboxConnectionId,
            cancellationToken);
        if (mailbox is null) return NotFound(new { error = "Boîte mail introuvable." });
        try
        {
            var result = await providerResolver.Resolve(mailbox.Provider)
                .SyncAsync(mailboxConnectionId, request.MaxResults, cancellationToken);
            return result is null
                ? Conflict(new { error = "Cette boîte mail n’est pas connectée." })
                : Ok(result);
        }
        catch (Exception exception) when (exception is GmailConfigurationException or OutlookConfigurationException)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is GmailApiException or OutlookApiException)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpDelete("{mailboxConnectionId:guid}")]
    public async Task<IActionResult> Delete(Guid mailboxConnectionId, CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == mailboxConnectionId,
            cancellationToken);
        if (mailbox is null) return NotFound();
        if (mailbox.EncryptedRefreshToken is not null)
        {
            return Conflict(new { error = "Déconnectez la boîte avant de la supprimer." });
        }
        if (await dbContext.MailboxConnections.CountAsync(cancellationToken) <= 1)
        {
            return Conflict(new { error = "La dernière boîte configurée ne peut pas être supprimée." });
        }
        dbContext.MailboxConnections.Remove(mailbox);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
