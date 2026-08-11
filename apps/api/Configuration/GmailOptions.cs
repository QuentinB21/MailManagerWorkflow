namespace MailManager.Api.Configuration;

public sealed class GmailOptions
{
    public const string SectionName = "Gmail";
    public const string ModifyScope = "https://www.googleapis.com/auth/gmail.modify";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:8080/api/gmail/oauth/callback";
    public string WebAppUrl { get; set; } = "http://localhost:5173";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
