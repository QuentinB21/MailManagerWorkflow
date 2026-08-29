namespace MailManager.Api.Configuration;

public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public int ProcessingLogsDays { get; set; } = 90;
    public int CleanupIntervalHours { get; set; } = 24;
}
