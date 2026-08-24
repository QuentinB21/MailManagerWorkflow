using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Services;
using MailManager.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/outlook")]
public sealed class OutlookController(
    MailManagerDbContext dbContext,
    OutlookOAuthService oauthService,
    OutlookMailboxService mailboxService,
    ILogger<OutlookController> logger,
    MailboxAccessService mailboxAccess,
    CurrentUser currentUser) : ControllerBase
{
    [HttpGet("configuration")]
    public ActionResult<ProviderConfigurationResponse> GetConfiguration() =>
        Ok(new ProviderConfigurationResponse(oauthService.IsConfigured));

    [HttpGet("oauth/authorization-url")]
    public async Task<IActionResult> Authorize(
        [FromQuery] Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo) return Forbid();
        if (!await mailboxAccess.CanAccessAsync(mailboxConnectionId, cancellationToken)) return NotFound();
        try
        {
            var url = await oauthService.CreateAuthorizationUrlAsync(mailboxConnectionId, cancellationToken);
            return url is null ? NotFound(new { error = "Boîte Outlook active introuvable." }) : Ok(new { url });
        }
        catch (OutlookConfigurationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [AllowAnonymous]
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect($"{oauthService.WebAppUrl}/?outlookError=access_denied");
        }
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
        {
            return Redirect($"{oauthService.WebAppUrl}/?outlookError=invalid_callback");
        }

        try
        {
            var mailboxId = await oauthService.CompleteAuthorizationAsync(state, code, cancellationToken);
            return Redirect($"{oauthService.WebAppUrl}/?outlook=connected&mailboxId={mailboxId}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "La connexion OAuth Outlook a échoué.");
            return Redirect($"{oauthService.WebAppUrl}/?outlookError=oauth_failed");
        }
    }

    [HttpGet("mailboxes/{mailboxConnectionId:guid}/test")]
    public async Task<ActionResult<MailboxConnectionTestResponse>> TestConnection(
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        if (!await mailboxAccess.CanAccessAsync(mailboxConnectionId, cancellationToken)) return NotFound();
        try
        {
            var result = await mailboxService.TestConnectionAsync(mailboxConnectionId, cancellationToken);
            return result is null
                ? Conflict(new { error = "Cette boîte Outlook n’est pas connectée." })
                : Ok(result);
        }
        catch (OutlookApiException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.Automation)]
    [HttpPost("mailboxes/{mailboxConnectionId:guid}/process-new")]
    public async Task<ActionResult<MailboxSyncResponse>> ProcessNew(
        Guid mailboxConnectionId,
        MailboxSyncRequest request,
        CancellationToken cancellationToken)
    {
        if (!await mailboxAccess.CanAccessAsync(mailboxConnectionId, cancellationToken)) return NotFound();
        try
        {
            var result = await mailboxService.SyncAsync(mailboxConnectionId, request.MaxResults, cancellationToken);
            return result is null
                ? Conflict(new { error = "Cette boîte Outlook n’est pas connectée." })
                : Ok(result);
        }
        catch (OutlookConfigurationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (OutlookApiException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("mailboxes/{mailboxConnectionId:guid}/disconnect")]
    public async Task<IActionResult> Disconnect(Guid mailboxConnectionId, CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo) return Forbid();
        var mailbox = await mailboxAccess.FindAsync(mailboxConnectionId, tracking: true, cancellationToken);
        if (mailbox?.Provider != MailProvider.Outlook) mailbox = null;
        if (mailbox is null) return NotFound(new { error = "Boîte Outlook introuvable." });

        mailbox.EncryptedRefreshToken = null;
        mailbox.EmailAddress = null;
        mailbox.GrantedScopes = null;
        mailbox.ConnectedAt = null;
        mailbox.LastSyncError = null;
        mailbox.RequiresReconnect = false;
        var labels = await dbContext.LabelDefinitions
            .Where(label => label.MailboxConnectionId == mailboxConnectionId)
            .ToListAsync(cancellationToken);
        foreach (var label in labels) label.ExternalLabelId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
