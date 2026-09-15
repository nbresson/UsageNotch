using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class MonitorMapTests
{
    private static MonitorInfo M(string id, bool primary, int x, int y, int w, int h, double scale = 1.0) =>
        new(id, primary, new PixelRect(x, y, w, h), new PixelRect(x, y, w, h - 48), scale);

    private static readonly MonitorInfo Main = M(@"\\.\DISPLAY1", true, 0, 0, 1920, 1080);
    private static readonly MonitorInfo Side = M(@"\\.\DISPLAY2", false, 1920, 0, 1920, 1080);

    private static void ShouldBe(MapRect actual, double x, double y, double w, double h)
    {
        actual.X.Should().BeApproximately(x, 1e-6);
        actual.Y.Should().BeApproximately(y, 1e-6);
        actual.Width.Should().BeApproximately(w, 1e-6);
        actual.Height.Should().BeApproximately(h, 1e-6);
    }

    [Fact]
    public void The_virtual_desktop_is_scaled_and_centred()
    {
        // Bureau 3840 × 1080 dans 200 × 100 avec une marge de 4 : facteur 0,05, contenu 192 × 54 centré.
        var map = MonitorMap.Layout([Side, Main], new Settings(), 200, 100, 4);

        map.Tiles.Select(t => t.Number).Should().Equal(1, 2);
        map.Tiles[0].DeviceId.Should().Be(@"\\.\DISPLAY1");
        map.Tiles[0].IsPrimary.Should().BeTrue();
        ShouldBe(map.Tiles[0].Rect, 4, 23, 96, 54);
        ShouldBe(map.Tiles[1].Rect, 100, 23, 96, 54);
    }

    [Fact]
    public void The_primary_monitor_is_selected_by_default_and_carries_the_pill_marker()
    {
        var map = MonitorMap.Layout([Main, Side], new Settings(), 200, 100, 4);

        map.Tiles.Select(t => t.IsSelected).Should().Equal(true, false);
        // Pilule 64 × 136 au bord droit, centrée : (1856, 472) sur l'écran principal.
        ShouldBe(map.PillMarker!.Value, 4 + 1856 * 0.05, 23 + 472 * 0.05, 64 * 0.05, 136 * 0.05);
    }

    [Fact]
    public void The_chosen_monitor_edge_and_position_move_the_marker()
    {
        var settings = new Settings { MonitorDeviceId = @"\\.\DISPLAY2", Edge = ScreenEdge.Left, PositionLeft = 0 };

        var map = MonitorMap.Layout([Main, Side], settings, 200, 100, 4);

        map.Tiles.Select(t => t.IsSelected).Should().Equal(false, true);
        ShouldBe(map.PillMarker!.Value, 100, 23, 64 * 0.05, 136 * 0.05);
    }

    [Fact]
    public void An_absent_monitor_falls_back_to_the_primary()
    {
        var map = MonitorMap.Layout([Main, Side], new Settings { MonitorDeviceId = @"\\.\DISPLAY9" }, 200, 100, 4);

        map.Tiles.Select(t => t.IsSelected).Should().Equal(true, false);
    }

    [Fact]
    public void Monitor_and_settings_scales_size_the_marker()
    {
        var hiDpi = M(@"\\.\DISPLAY1", true, 0, 0, 1920, 1080, 1.5);

        var map = MonitorMap.Layout([hiDpi], new Settings { Scale = 0.5 }, 200, 100, 4);

        // Échelle physique 1,5 × 0,5 = 0,75 : épaisseur round(64 × 0,75) = 48, longueur round(136 × 0,75) = 102.
        var factor = Math.Min(192.0 / 1920, 92.0 / 1080);
        map.PillMarker!.Value.Width.Should().BeApproximately(48 * factor, 1e-6);
        map.PillMarker!.Value.Height.Should().BeApproximately(102 * factor, 1e-6);
    }

    [Fact]
    public void A_tiny_marker_is_enlarged_against_its_edge()
    {
        var uhd = M(@"\\.\DISPLAY1", true, 0, 0, 3840, 2160);

        var map = MonitorMap.Layout([uhd], new Settings(), 100, 60, 0);

        var marker = map.PillMarker!.Value;
        marker.Width.Should().Be(MonitorMap.MinMarkerSize);
        (marker.X + marker.Width).Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void No_monitor_gives_an_empty_map()
    {
        MonitorMap.Layout([], new Settings(), 200, 100, 4).Should().Be(MonitorMapModel.Empty);
    }
}
