using System.Text.Json;
using System.Text.Json.Serialization;
using MailManager.Api.Domain;

namespace MailManager.Api.Mcp;

public sealed record DestinationChange(
    [property: JsonRequired] string Key, [property: JsonRequired] Guid? Id,
    [property: JsonRequired] string Name, [property: JsonRequired] string? Color,
    [property: JsonRequired] bool IsActive);
public sealed record RuleChange(
    [property: JsonRequired] Guid? Id, [property: JsonRequired] string Name,
    [property: JsonRequired] string DestinationKey, [property: JsonRequired] int Priority,
    [property: JsonRequired] bool IsActive, [property: JsonRequired] MatchMode MatchMode,
    [property: JsonRequired] string[]? SenderAddresses, [property: JsonRequired] string[]? SenderDomains,
    [property: JsonRequired] string[]? SubjectKeywords, [property: JsonRequired] string[]? BodyKeywords);
public sealed record ConfigurationChanges([property: JsonRequired] DestinationChange[] Destinations, [property: JsonRequired] RuleChange[] Rules);
public sealed record DestinationState(Guid Id, string Name, string? Color, bool IsActive);
public sealed record RuleState(Guid Id, string Name, Guid DestinationId, int Priority, bool IsActive,
    MatchMode MatchMode, string[] SenderAddresses, string[] SenderDomains, string[] SubjectKeywords, string[] BodyKeywords,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ConfigurationState(Guid MailboxId, string MailboxName, string Provider, bool IsActive,
    DestinationState[] Destinations, RuleState[] Rules);
public sealed record ProposalView(Guid ProposalId, string Status, DateTimeOffset ExpiresAt, DateTimeOffset? AppliedAt,
    ConfigurationState Before, ConfigurationState After, Guid[] ChangedDestinationIds, Guid[] ChangedRuleIds,
    string[] Warnings, string SynchronizationStatus);

public static class McpJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() }
    };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}
