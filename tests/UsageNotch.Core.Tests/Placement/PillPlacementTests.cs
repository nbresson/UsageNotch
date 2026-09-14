using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Tests.Placement;

public class PillPlacementTests
{
    // Écran 2560×1440 à 150 %, placé à droite d'un écran principal 1920 px de large
    private static readonly PixelRect Monitor = new(1920, 0, 2560, 1440);
    private const int Length = 300;
    private const int Thickness = 100;

    [Theory]
    [InlineData(ScreenEdge.Right, true)]
    [InlineData(ScreenEdge.Left, true)]
    [InlineData(ScreenEdge.Top, false)]
    [InlineData(ScreenEdge.Bottom, false)]
    public void Vertical_on_the_sides_horizontal_top_and_bottom(ScreenEdge edge, bool vertical) =>
        PillPlacement.IsVertical(edge).Should().Be(vertical);

    [Fact]
    public void Right_edge_at_the_middle_is_flush_and_centred()
    {
        var r = PillPlacement.PillRect(Monitor, ScreenEdge.Right, 0.5, Length, Thickness);
        r.Should().Be(new PixelRect(1920 + 2560 - 100, 570, 100, 300));
    }

    [Fact]
    public void Left_edge_extremes_stay_inside_the_monitor()
    {
        PillPlacement.PillRect(Monitor, ScreenEdge.Left, 0.0, Length, Thickness).Should().Be(new PixelRect(1920, 0, 100, 300));
        PillPlacement.PillRect(Monitor, ScreenEdge.Left, 1.0, Length, Thickness).Should().Be(new PixelRect(1920, 1140, 100, 300));
        PillPlacement.PillRect(Monitor, ScreenEdge.Left, 7.0, Length, Thickness).Y.Should().Be(1140, "la fraction est bornée");
    }

    [Fact]
    public void Top_and_bottom_lay_the_pill_along_the_horizontal_edge()
    {
        PillPlacement.PillRect(Monitor, ScreenEdge.Top, 0.5, Length, Thickness).Should().Be(new PixelRect(1920 + 1130, 0, 300, 100));
        PillPlacement.PillRect(Monitor, ScreenEdge.Bottom, 0.0, Length, Thickness).Should().Be(new PixelRect(1920, 1340, 300, 100));
    }

    [Fact]
    public void Fraction_is_the_inverse_of_placement()
    {
        foreach (var edge in Enum.GetValues<ScreenEdge>())
        {
            var pill = PillPlacement.PillRect(Monitor, edge, 0.3, Length, Thickness);
            PillPlacement.FractionOf(Monitor, edge, pill).Should().BeApproximately(0.3, 0.001, $"bord {edge}");
        }
    }

    [Fact]
    public void Card_opens_toward_the_centre_and_is_centred_on_the_anchor()
    {
        var pill = PillPlacement.PillRect(Monitor, ScreenEdge.Right, 0.5, Length, Thickness);
        var anchor = new PixelRect(pill.X + 20, pill.Y + 20, 60, 60); // première cellule
        var card = PillPlacement.CardRect(Monitor, ScreenEdge.Right, pill, anchor, cardWidth: 400, cardHeight: 200, gap: 12, margin: 8);

        card.Right.Should().Be(pill.X - 12);
        card.Width.Should().Be(400);
        card.CenterY.Should().Be(anchor.CenterY);

        var left = PillPlacement.PillRect(Monitor, ScreenEdge.Left, 0.5, Length, Thickness);
        PillPlacement.CardRect(Monitor, ScreenEdge.Left, left, anchor with { X = left.X + 20 }, 400, 200, 12, 8).X.Should().Be(left.Right + 12);

        var top = PillPlacement.PillRect(Monitor, ScreenEdge.Top, 0.5, Length, Thickness);
        PillPlacement.CardRect(Monitor, ScreenEdge.Top, top, new PixelRect(top.X + 20, top.Y + 20, 60, 60), 400, 200, 12, 8).Y.Should().Be(top.Bottom + 12);

        var bottom = PillPlacement.PillRect(Monitor, ScreenEdge.Bottom, 0.5, Length, Thickness);
        PillPlacement.CardRect(Monitor, ScreenEdge.Bottom, bottom, new PixelRect(bottom.X + 20, bottom.Y + 20, 60, 60), 400, 200, 12, 8).Bottom.Should().Be(bottom.Y - 12);
    }

    [Fact]
    public void Card_is_kept_inside_the_monitor_with_a_margin()
    {
        var pill = PillPlacement.PillRect(Monitor, ScreenEdge.Right, 0.0, Length, Thickness);
        var anchor = new PixelRect(pill.X + 20, pill.Y + 4, 60, 60);

        var card = PillPlacement.CardRect(Monitor, ScreenEdge.Right, pill, anchor, 400, 600, 12, 8);

        card.Y.Should().Be(Monitor.Y + 8);
        card.Bottom.Should().BeLessThanOrEqualTo(Monitor.Bottom - 8);
    }

    [Fact]
    public void Choose_prefers_the_remembered_monitor_then_the_primary_then_the_first()
    {
        var primary = new MonitorInfo(@"\\.\DISPLAY1", true, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1.0);
        var second = new MonitorInfo(@"\\.\DISPLAY2", false, Monitor, Monitor, 1.5);
        var monitors = new[] { second, primary };

        PillPlacement.Choose(monitors, @"\\.\DISPLAY2").Should().Be(second);
        PillPlacement.Choose(monitors, @"\\.\DISPLAY9").Should().Be(primary);
        PillPlacement.Choose(monitors, null).Should().Be(primary);
        PillPlacement.Choose([second], null).Should().Be(second);
    }

    [Fact]
    public void Choose_throws_without_monitors()
    {
        var act = () => PillPlacement.Choose([], null);
        act.Should().Throw<InvalidOperationException>();
    }
}
