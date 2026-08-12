namespace MailManager.Api.Configuration;

public sealed class OutlookOptions
{
    public const string SectionName = "Outlook";
    public const string Scopes = "offline_access User.Read Mail.ReadWrite MailboxSettings.ReadWrite";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Tenant { get; set; } = "common";
    public string RedirectUri { get; set; } = "http://localhost:8080/api/outlook/oauth/callback";
    public string WebAppUrl { get; set; } = "http://localhost:5173";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
