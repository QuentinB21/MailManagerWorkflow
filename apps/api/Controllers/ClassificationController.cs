using MailManager.Api.Contracts;
using MailManager.Api.Services;
using MailManager.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/classification")]
public sealed class ClassificationController(
    EmailProcessingService processingService,
    MailboxAccessService mailboxAccess,
    CurrentUser currentUser) : ControllerBase
{
    [HttpPost("simulate")]
    public async Task<ActionResult<ClassificationResultResponse>> Simulate(
        NormalizedEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (!await mailboxAccess.CanAccessAsync(request.MailboxConnectionId, cancellationToken)) return NotFound();
        var result = await processingService.SimulateAsync(request, cancellationToken);
        return result is null
            ? NotFound(new { error = "Boîte mail active introuvable." })
            : Ok(result);
    }

    [HttpPost("process")]
    public async Task<ActionResult<ClassificationResultResponse>> Process(
        NormalizedEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo) return Forbid();
        if (!await mailboxAccess.CanAccessAsync(request.MailboxConnectionId, cancellationToken)) return NotFound();
        var result = await processingService.ProcessAsync(request, cancellationToken);
        return result is null
            ? NotFound(new { error = "Boîte mail active introuvable." })
            : Ok(result);
    }
}
