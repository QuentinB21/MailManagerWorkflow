using System.Text.RegularExpressions;

namespace MailManager.Api.Services;

public static partial class RuleValueNormalizer
{
    public static string Text(string? value) =>
        WhitespaceRegex().Replace(value?.Trim() ?? string.Empty, " ").ToLowerInvariant();

    public static string[] Values(IEnumerable<string>? values, bool domain = false) =>
        (values ?? [])
            .Select(Text)
            .Select(x => domain ? x.TrimStart('@') : x)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
