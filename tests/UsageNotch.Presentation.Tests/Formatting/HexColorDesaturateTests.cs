using FluentAssertions;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Formatting;

public class HexColorDesaturateTests
{
    [Theory]
    // Luminance Rec. 709 : 0,2126 R + 0,7152 V + 0,0722 B, arrondie.
    [InlineData("#FFBF00", "#BFBFBF")]   // ambre « en attente »
    [InlineData("#28E07B", "#B2B2B2")]   // vert « en cours »
    [InlineData("#D97757", "#8A8A8A")]   // terracotta « terminé »
    [InlineData("#000000", "#000000")]
    [InlineData("#FFFFFF", "#FFFFFF")]
    public void A_colour_becomes_the_grey_of_the_same_perceived_lightness(string hex, string grey) =>
        HexColor.Desaturate(hex).Should().Be(grey);

    [Fact]
    public void A_grey_desaturates_to_itself()
    {
        HexColor.Desaturate("#808080").Should().Be("#808080");
        HexColor.Desaturate("#3A3A3A").Should().Be("#3A3A3A");
    }

    [Fact]
    public void The_green_channel_weighs_most()
    {
        var fromGreen = HexColor.Desaturate("#00FF00");
        var fromBlue = HexColor.Desaturate("#0000FF");

        fromGreen.Should().Be("#B6B6B6");
        fromBlue.Should().Be("#121212");
    }

    [Fact]
    public void An_unreadable_colour_is_refused()
    {
        var act = () => HexColor.Desaturate("bleu");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_loose_form_is_accepted_like_everywhere_else()
    {
        HexColor.Desaturate(" fff ").Should().Be("#FFFFFF");
    }
}
