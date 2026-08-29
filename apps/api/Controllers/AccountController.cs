using System.Text.Json;
using System.Text.Json.Serialization;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Security;
using MailManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(
    MailManagerDbContext dbContext,
    CurrentUser currentUser,
    AccountDataService accountDataService) : ControllerBase
{
    [HttpGet("legal-status")]
    public async Task<ActionResult<LegalStatusResponse>> GetLegalStatus(CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo)
        {
            return Ok(new LegalStatusResponse(true, LegalDocumentVersions.Terms,
                LegalDocumentVersions.Privacy, null));
        }

        var acceptance = await dbContext.LegalAcceptances
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerSubject == currentUser.Subject, cancellationToken);
        var isAccepted = acceptance?.TermsVersion == LegalDocumentVersions.Terms
            && acceptance.PrivacyVersion == LegalDocumentVersions.Privacy;
        return Ok(new LegalStatusResponse(isAccepted, LegalDocumentVersions.Terms,
            LegalDocumentVersions.Privacy, acceptance?.AcceptedAt));
    }

    [HttpPost("legal-acceptance")]
    public async Task<ActionResult<LegalStatusResponse>> AcceptLegalDocuments(
        AcceptLegalDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo) return Forbid();
        if (!request.AcceptTerms || !request.AcknowledgePrivacy)
        {
            return BadRequest(new
            {
                error = "Vous devez accepter les conditions d'utilisation et reconnaître avoir lu la politique de confidentialité."
            });
        }

        var acceptance = await dbContext.LegalAcceptances
            .SingleOrDefaultAsync(item => item.OwnerSubject == currentUser.Subject, cancellationToken);
        if (acceptance is null)
        {
            acceptance = new LegalAcceptance
            {
                Id = Guid.NewGuid(),
                OwnerSubject = currentUser.Subject,
                TermsVersion = LegalDocumentVersions.Terms,
                PrivacyVersion = LegalDocumentVersions.Privacy,
                AcceptedAt = DateTimeOffset.UtcNow
            };
            dbContext.LegalAcceptances.Add(acceptance);
        }
        else
        {
            acceptance.TermsVersion = LegalDocumentVersions.Terms;
            acceptance.PrivacyVersion = LegalDocumentVersions.Privacy;
            acceptance.AcceptedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new LegalStatusResponse(true, acceptance.TermsVersion,
            acceptance.PrivacyVersion, acceptance.AcceptedAt));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo) return Forbid();
        var export = await accountDataService.ExportAsync(cancellationToken);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var contents = JsonSerializer.SerializeToUtf8Bytes(export, options);
        var filename = $"mail-manager-export-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json";
        return File(contents, "application/json", filename);
    }

    [HttpDelete("data")]
    public async Task<IActionResult> DeleteApplicationData(CancellationToken cancellationToken)
    {
        if (currentUser.IsDemo) return Forbid();
        await accountDataService.DeleteApplicationDataAsync(cancellationToken);
        return NoContent();
    }
}
