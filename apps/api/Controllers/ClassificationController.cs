using MailManager.Api.Contracts;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/classification")]
public sealed class ClassificationController(EmailProcessingService processingService) : ControllerBase
{
    [HttpPost("simulate")]
    public async Task<ActionResult<ClassificationResultResponse>> Simulate(
        NormalizedEmailRequest request,
        CancellationToken cancellationToken)
    {
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
        var result = await processingService.ProcessAsync(request, cancellationToken);
        return result is null
            ? NotFound(new { error = "Boîte mail active introuvable." })
            : Ok(result);
    }
}
