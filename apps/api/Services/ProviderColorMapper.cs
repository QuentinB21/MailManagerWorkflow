using System.Globalization;

namespace MailManager.Api.Services;

public readonly record struct GmailLabelColor(string TextColor, string BackgroundColor);

public static class ProviderColorMapper
{
    private static readonly string[] GmailBackgroundColors =
    [
        "#000000", "#434343", "#666666", "#999999", "#cccccc", "#efefef", "#f3f3f3", "#ffffff",
        "#fb4c2f", "#ffad47", "#fad165", "#16a766", "#43d692", "#4a86e8", "#a479e2", "#f691b3",
        "#f6c5be", "#ffe6c7", "#fef1d1", "#b9e4d0", "#c6f3de", "#c9daf8", "#e4d7f5", "#fcdee8",
        "#efa093", "#ffd6a2", "#fce8b3", "#89d3b2", "#a0eac9", "#a4c2f4", "#d0bcf1", "#fbc8d9",
        "#e66550", "#ffbc6b", "#fcda83", "#44b984", "#68dfa9", "#6d9eeb", "#b694e8", "#f7a7c0",
        "#cc3a21", "#eaa041", "#f2c960", "#149e60", "#3dc789", "#3c78d8", "#8e63ce", "#e07798",
        "#ac2b16", "#cf8933", "#d5ae49", "#0b804b", "#2a9c68", "#285bac", "#653e9b", "#b65775",
        "#822111", "#a46a21", "#aa8831", "#076239", "#1a764d", "#1c4587", "#41236d", "#83334c",
        "#464646", "#e7e7e7", "#0d3472", "#b6cff5", "#0d3b44", "#98d7e4", "#3d188e", "#e3d7ff",
        "#711a36", "#fbd3e0", "#8a1c0a", "#f2b2a8", "#7a2e0b", "#ffc8af", "#7a4706", "#ffdeb5",
        "#594c05", "#fbe983", "#684e07", "#fdedc1", "#0b4f30", "#b3efd3", "#04502e", "#a2dcc1",
        "#c2c2c2", "#4986e7", "#2da2bb", "#b99aff", "#994a64", "#f691b2", "#ff7537", "#ffad46",
        "#662e37", "#ebdbde", "#cca6ac", "#094228", "#42d692", "#16a765"
    ];

    private static readonly (string Preset, string Hex)[] OutlookColors =
    [
        ("preset0", "#e74856"), ("preset1", "#ff8c00"), ("preset2", "#a2845e"),
        ("preset3", "#f9d71c"), ("preset4", "#00cc6a"), ("preset5", "#00b7c3"),
        ("preset6", "#8e8b00"), ("preset7", "#0078d4"), ("preset8", "#8764b8"),
        ("preset9", "#c239b3"), ("preset10", "#7a7574"), ("preset11", "#5d5a58"),
        ("preset12", "#8a8886"), ("preset13", "#4b4a48"), ("preset14", "#000000"),
        ("preset15", "#a4262c"), ("preset16", "#ca5010"), ("preset17", "#705a3a"),
        ("preset18", "#c19c00"), ("preset19", "#0b6a0b"), ("preset20", "#038387"),
        ("preset21", "#5f6b2c"), ("preset22", "#004e8c"), ("preset23", "#5c2d91"),
        ("preset24", "#9b0062")
    ];

    public static string? NormalizeHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#') return null;
        return int.TryParse(trimmed.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? trimmed.ToLowerInvariant()
            : null;
    }

    public static GmailLabelColor? ToGmail(string? requestedColor)
    {
        var normalized = NormalizeHexColor(requestedColor);
        if (normalized is null) return null;

        var background = FindNearest(normalized, GmailBackgroundColors);
        var (red, green, blue) = Parse(background);
        var luminance = (0.2126 * Linear(red)) + (0.7152 * Linear(green)) + (0.0722 * Linear(blue));
        var text = luminance > 0.42 ? "#000000" : "#ffffff";
        return new GmailLabelColor(text, background);
    }

    public static string ToOutlookPreset(string? requestedColor)
    {
        var normalized = NormalizeHexColor(requestedColor);
        if (normalized is null) return "preset0";

        return OutlookColors
            .MinBy(candidate => ColorDistance(normalized, candidate.Hex))
            .Preset;
    }

    private static string FindNearest(string color, IEnumerable<string> candidates) =>
        candidates.MinBy(candidate => ColorDistance(color, candidate))!;

    private static double ColorDistance(string first, string second)
    {
        var (red1, green1, blue1) = Parse(first);
        var (red2, green2, blue2) = Parse(second);
        var redMean = (red1 + red2) / 2d;
        var redDelta = red1 - red2;
        var greenDelta = green1 - green2;
        var blueDelta = blue1 - blue2;
        return ((2 + redMean / 256) * redDelta * redDelta)
            + (4 * greenDelta * greenDelta)
            + ((2 + (255 - redMean) / 256) * blueDelta * blueDelta);
    }

    private static (int Red, int Green, int Blue) Parse(string color) =>
        (Convert.ToInt32(color.Substring(1, 2), 16),
         Convert.ToInt32(color.Substring(3, 2), 16),
         Convert.ToInt32(color.Substring(5, 2), 16));

    private static double Linear(int channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
