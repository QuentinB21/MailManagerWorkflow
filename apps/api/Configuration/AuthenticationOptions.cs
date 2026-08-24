namespace MailManager.Api.Configuration;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string MetadataAddress { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = "mail-manager-api";
    public bool RequireHttpsMetadata { get; set; }
}

