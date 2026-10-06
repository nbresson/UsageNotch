using FluentAssertions;
using UsageNotch.App.Views;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Tests.Views;

public class NotchPlacerTests
{
    private sealed class FakeMonitorSource(IReadOnlyList<MonitorInfo> monitors) : IMonitorSource
    {
        public IReadOnlyList<MonitorInfo> GetMonitors() => monitors;
    }

    private static readonly MonitorInfo Primary = new(
        DeviceId: @"\\.\DISPLAY1",
        IsPrimary: true,
        Bounds: new PixelRect(0, 0, 1920, 1080),
        WorkArea: new PixelRect(0, 0, 1920, 1040),
        Scale: 1.0);

    [Fact]
    public void Compute_places_pill_on_screen_edge()
    {
        var monitors = new FakeMonitorSource([Primary]);
        var placer = new NotchPlacer(monitors);
        var settings = new Settings { Edge = ScreenEdge.Right, PositionRight = 0.5, Scale = 1.0 };

        var result = placer.Compute(settings);

        result.Monitor.Should().Be(Primary);
        result.PillRect.Right.Should().Be(1920);
        result.PillRect.Y.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FractionForCursor_computes_fraction_centered_on_cursor()
    {
        var monitors = new FakeMonitorSource([Primary]);
        var placer = new NotchPlacer(monitors);
        var settings = new Settings { Edge = ScreenEdge.Right, PositionRight = 0.5 };
        var placement = placer.Compute(settings);

        var fraction = placer.FractionForCursor(placement, ScreenEdge.Right, 1920, 540);
        fraction.Should().BeApproximately(0.5, 0.05);
    }

    [Fact]
    public void CardRect_positions_card_adjacent_to_pill()
    {
        var monitors = new FakeMonitorSource([Primary]);
        var placer = new NotchPlacer(monitors);
        var settings = new Settings { Edge = ScreenEdge.Right, PositionRight = 0.5 };
        var placement = placer.Compute(settings);

        var card = placer.CardRect(placement, settings, 350, 200);
        card.Right.Should().BeLessThanOrEqualTo(placement.PillRect.X);
        card.Width.Should().Be(350);
        card.Height.Should().Be(200);
    }
}
