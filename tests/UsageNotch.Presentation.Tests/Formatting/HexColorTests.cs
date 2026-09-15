using FluentAssertions;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Formatting;

public class HexColorTests
{
    [Theory]
    [InlineData("#28e07b", "#28E07B")]
    [InlineData("28E07B", "#28E07B")]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("fff", "#FFFFFF")]
    [InlineData("  #FF4500 ", "#FF4500")]
    public void Valid_entries_are_normalised_to_uppercase_six_digits(string input, string expected)
    {
        HexColor.TryNormalize(input, out var hex).Should().BeTrue();
        hex.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("rouge")]
    public void Invalid_entries_are_rejected(string? input)
    {
        HexColor.TryNormalize(input, out var hex).Should().BeFalse();
        hex.Should().BeEmpty();
    }

    [Fact]
    public void Colorref_is_blue_green_red()
    {
        HexColor.ToColorRef("#FF4500").Should().Be(0x000045FFu);
        HexColor.FromColorRef(0x000045FFu).Should().Be("#FF4500");
    }

    [Fact]
    public void Colorref_round_trips()
    {
        HexColor.FromColorRef(HexColor.ToColorRef("#28e07b")).Should().Be("#28E07B");
    }

    [Fact]
    public void Colorref_of_an_invalid_colour_throws()
    {
        var act = () => HexColor.ToColorRef("rouge");
        act.Should().Throw<ArgumentException>();
    }
}
