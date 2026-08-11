using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/gmail")]
public sealed class GmailController(
    MailManagerDbContext dbContext,
    GmailOAuthService oauthService,
    GmailOAuthConfigurationService configurationService,
    GmailMailboxService gmailMailboxService,
    ILogger<GmailController> logger) : ControllerBase
{
    [HttpGet("configuration")]
    public async Task<ActionResult<GmailOAuthConfigurationResponse>> GetConfiguration(
        CancellationToken cancellationToken) =>
        Ok(await configurationService.GetStatusAsync(cancellationToken));

    [HttpPut("configuration")]
    public async Task<ActionResult<GmailOAuthConfigurationResponse>> SaveConfiguration(
        GmailOAuthConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (await dbContext.MailboxConnections.AnyAsync(
                mailbox => mailbox.EncryptedRefreshToken != null,
                cancellationToken))
        {
            return Conflict(new { error = "Déconnectez d’abord la boîte Gmail avant de modifier la configuration OAuth." });
        }

        try
        {
            return Ok(await configurationService.SaveAsync(request, cancellationToken));
        }
        catch (GmailConfigurationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("configuration")]
    public async Task<IActionResult> DeleteConfiguration(CancellationToken cancellationToken)
    {
        if (await dbContext.MailboxConnections.AnyAsync(
                mailbox => mailbox.EncryptedRefreshToken != null,
                cancellationToken))
        {
            return Conflict(new { error = "Déconnectez d’abord la boîte Gmail avant de supprimer la configuration OAuth." });
        }

        return await configurationService.DeleteAsync(cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("oauth/authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorizationUrl = await oauthService.CreateAuthorizationUrlAsync(
                mailboxConnectionId,
                cancellationToken);
            return authorizationUrl is null
                ? NotFound(new { error = "Boîte mail active introuvable." })
                : Redirect(authorizationUrl);
        }
        catch (GmailConfigurationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect($"{oauthService.WebAppUrl}/?gmailError=access_denied");
        }
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
        {
            return Redirect($"{oauthService.WebAppUrl}/?gmailError=invalid_callback");
        }

        try
        {
            await oauthService.CompleteAuthorizationAsync(state, code, cancellationToken);
            return Redirect($"{oauthService.WebAppUrl}/?gmail=connected");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "La connexion OAuth Gmail a échoué.");
            return Redirect($"{oauthService.WebAppUrl}/?gmailError=oauth_failed");
        }
    }

    [HttpGet("mailboxes/{mailboxConnectionId:guid}/test")]
    public async Task<ActionResult<GmailConnectionTestResponse>> TestConnection(
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await gmailMailboxService.TestConnectionAsync(mailboxConnectionId, cancellationToken);
            return result is null
                ? Conflict(new { error = "Cette boîte Gmail n’est pas connectée." })
                : Ok(result);
        }
        catch (GmailApiException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("mailboxes/{mailboxConnectionId:guid}/process-unread")]
    public async Task<ActionResult<GmailSyncResponse>> ProcessUnread(
        Guid mailboxConnectionId,
        GmailSyncRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await gmailMailboxService.ProcessUnreadAsync(
                mailboxConnectionId,
                request.MaxResults,
                cancellationToken);
            return result is null
                ? Conflict(new { error = "Cette boîte Gmail n’est pas connectée." })
                : Ok(result);
        }
        catch (GmailConfigurationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (GmailApiException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("mailboxes/{mailboxConnectionId:guid}/disconnect")]
    public async Task<IActionResult> Disconnect(
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == mailboxConnectionId,
            cancellationToken);
        if (mailbox is null) return NotFound(new { error = "Boîte mail introuvable." });

        if (!string.IsNullOrWhiteSpace(mailbox.EncryptedRefreshToken))
        {
            try
            {
                await oauthService.DisconnectAsync(mailbox.EncryptedRefreshToken, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogInformation(exception, "Le jeton Gmail était déjà révoqué ou inaccessible.");
            }
        }

        mailbox.EncryptedRefreshToken = null;
        mailbox.EmailAddress = null;
        mailbox.GrantedScopes = null;
        mailbox.ConnectedAt = null;
        mailbox.LastSyncError = null;
        var labels = await dbContext.LabelDefinitions
            .Where(label => label.MailboxConnectionId == mailboxConnectionId)
            .ToListAsync(cancellationToken);
        foreach (var label in labels) label.ExternalLabelId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
