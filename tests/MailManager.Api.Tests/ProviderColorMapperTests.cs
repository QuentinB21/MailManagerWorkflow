using MailManager.Api.Services;

namespace MailManager.Api.Tests;

public sealed class ProviderColorMapperTests
{
    [Fact]
    public void Gmail_keeps_an_allowed_color_and_selects_readable_text()
    {
        var color = ProviderColorMapper.ToGmail("#4a86e8");

        Assert.NotNull(color);
        Assert.Equal("#4a86e8", color.Value.BackgroundColor);
        Assert.Equal("#ffffff", color.Value.TextColor);
    }

    [Fact]
    public void Gmail_maps_an_arbitrary_color_to_its_nearest_supported_color()
    {
        var color = ProviderColorMapper.ToGmail("#4f46e5");

        Assert.NotNull(color);
        Assert.NotEqual("#4f46e5", color.Value.BackgroundColor);
    }

    [Fact]
    public void Outlook_maps_red_to_the_red_preset()
    {
        Assert.Equal("preset0", ProviderColorMapper.ToOutlookPreset("#ef4444"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("purple")]
    [InlineData("#123")]
    public void Invalid_or_missing_colors_are_handled_safely(string? value)
    {
        Assert.Null(ProviderColorMapper.ToGmail(value));
        Assert.Equal("preset0", ProviderColorMapper.ToOutlookPreset(value));
    }
}
