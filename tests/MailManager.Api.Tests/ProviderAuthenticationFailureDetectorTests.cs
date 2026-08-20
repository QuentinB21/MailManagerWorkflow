using MailManager.Api.Services;

namespace MailManager.Api.Tests;

public sealed class ProviderAuthenticationFailureDetectorTests
{
    [Fact]
    public void RequiresReconnect_DetectsGoogleInvalidGrant()
    {
        var exception = new GmailApiException(
            "Google a refusé la requête (400).",
            "{\"error\":\"invalid_grant\",\"error_description\":\"Token has been expired or revoked.\"}");

        Assert.True(ProviderAuthenticationFailureDetector.RequiresReconnect(exception));
    }

    [Fact]
    public void RequiresReconnect_DetectsMicrosoftInvalidGrant()
    {
        var exception = new OutlookApiException(
            "Microsoft a refusé la requête (400).",
            "{\"error\":\"invalid_grant\",\"error_description\":\"AADSTS700082\"}");

        Assert.True(ProviderAuthenticationFailureDetector.RequiresReconnect(exception));
    }

    [Fact]
    public void RequiresReconnect_IgnoresTransientProviderFailure()
    {
        var exception = new GmailApiException(
            "Google a refusé la requête (503).",
            "{\"error\":{\"code\":503,\"status\":\"UNAVAILABLE\"}}");

        Assert.False(ProviderAuthenticationFailureDetector.RequiresReconnect(exception));
    }
}
