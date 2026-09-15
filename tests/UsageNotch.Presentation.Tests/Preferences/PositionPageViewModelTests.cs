using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class PositionPageViewModelTests
{
    private static MonitorInfo M(string id, bool primary, int x, int w, int h, double scale) =>
        new(id, primary, new PixelRect(x, 0, w, h), new PixelRect(x, 0, w, h - 48), scale);

    private static readonly MonitorInfo Main = M(@"\\.\DISPLAY1", true, 0, 2560, 1440, 1.5);
    private static readonly MonitorInfo Side = M(@"\\.\DISPLAY2", false, 2560, 1920, 1080, 1.0);

    private static (DraftFixture F, PositionPageViewModel Vm, FakeMonitors Monitors) Create(Func<Settings, Settings>? initial = null)
    {
        var f = new DraftFixture(initial);
        var monitors = new FakeMonitors();
        monitors.Monitors.AddRange([Main, Side]);
        return (f, new PositionPageViewModel(f.Draft, monitors), monitors);
    }

    [Fact]
    public void Lists_the_primary_choice_then_each_monitor()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Monitors.Select(c => c.Value).Should().Equal("", @"\\.\DISPLAY1", @"\\.\DISPLAY2");
        vm.MonitorKey.Should().Be(MonitorChoices.PrimaryKey);
        vm.Edges.Should().BeSameAs(Choices.Edges);
        vm.Visibilities.Should().BeSameAs(Choices.Visibilities);
    }

    [Fact]
    public void Choosing_a_monitor_stores_its_id_and_the_primary_choice_stores_null()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.MonitorKey = @"\\.\DISPLAY2";
        f.Draft.Value.MonitorDeviceId.Should().Be(@"\\.\DISPLAY2");
        vm.Map.Tiles.Select(t => t.IsSelected).Should().Equal(false, true);

        vm.MonitorKey = MonitorChoices.PrimaryKey;
        f.Draft.Value.MonitorDeviceId.Should().BeNull();
    }

    [Fact]
    public void A_null_selection_from_the_list_is_ignored()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.MonitorKey = null!;

        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Clicking_a_tile_selects_its_monitor()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.SelectMonitorCommand.Execute(@"\\.\DISPLAY2");

        f.Draft.Value.MonitorDeviceId.Should().Be(@"\\.\DISPLAY2");
    }

    [Fact]
    public void An_absent_saved_monitor_stays_selected_while_the_map_shows_the_fallback()
    {
        var (f, vm, _) = Create(s => s with { MonitorDeviceId = @"\\.\DISPLAY9" });
        using var _f = f;

        vm.Monitors.Should().HaveCount(4);
        vm.MonitorKey.Should().Be(@"\\.\DISPLAY9");
        vm.Map.Tiles.Select(t => t.IsSelected).Should().Equal(true, false);
    }

    [Fact]
    public void A_saved_monitor_id_with_another_case_selects_the_listed_monitor()
    {
        var (f, vm, _) = Create(s => s with { MonitorDeviceId = @"\\.\display2" });
        using var _f = f;

        vm.Monitors.Should().HaveCount(3);
        vm.MonitorKey.Should().Be(@"\\.\DISPLAY2");
    }

    [Fact]
    public void Each_edge_keeps_its_own_position()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Position = 0.2;
        vm.Edge = ScreenEdge.Top;

        vm.Position.Should().Be(0.5);
        vm.Position = 0.9;

        f.Draft.Value.PositionRight.Should().Be(0.2);
        f.Draft.Value.PositionTop.Should().Be(0.9);
        vm.PositionText.Should().Be("90" + FrenchText.Nbsp + "%");
    }

    [Fact]
    public void Recenter_puts_the_pill_back_in_the_middle_of_the_current_edge()
    {
        var (f, vm, _) = Create(s => s with { Edge = ScreenEdge.Bottom, PositionBottom = 0.1, PositionRight = 0.3 });
        using var _f = f;

        vm.RecenterCommand.Execute(null);

        f.Draft.Value.PositionBottom.Should().Be(0.5);
        f.Draft.Value.PositionRight.Should().Be(0.3);
    }

    [Fact]
    public void Folded_thickness_is_enabled_only_in_folded_mode_and_clamped()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.FoldedThicknessEnabled.Should().BeFalse();
        vm.VisibilityHint.Should().BeEmpty();

        vm.Visibility = VisibilityMode.Folded;
        vm.FoldedThickness = 20;

        vm.FoldedThicknessEnabled.Should().BeTrue();
        vm.VisibilityHint.Should().Be(PositionPageViewModel.FoldedHint);
        f.Draft.Value.FoldedThicknessPx.Should().Be(12);
        vm.FoldedThickness.Should().Be(12);
        vm.FoldedThicknessText.Should().Be("12 px");

        vm.FoldedThickness = 6.6;
        f.Draft.Value.FoldedThicknessPx.Should().Be(7);
    }

    [Fact]
    public void Hidden_mode_explains_that_only_the_tray_icon_remains()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Visibility = VisibilityMode.Hidden;

        vm.VisibilityHint.Should().Be(PositionPageViewModel.HiddenHint);
        vm.FoldedThicknessEnabled.Should().BeFalse();
    }

    [Fact]
    public void Hiding_in_fullscreen_edits_the_draft_and_does_not_apply_in_hidden_mode()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.HideInFullscreen.Should().BeTrue();
        vm.HideInFullscreenEditable.Should().BeTrue();

        vm.HideInFullscreen = false;
        f.Draft.Value.HideInFullscreen.Should().BeFalse();

        vm.Visibility = VisibilityMode.Hidden;
        vm.HideInFullscreenEditable.Should().BeFalse();
    }

    [Fact]
    public void Refreshing_monitors_rebuilds_the_list_only_when_it_changed()
    {
        var (f, vm, monitors) = Create();
        using var _f = f;
        var before = vm.Monitors;

        vm.RefreshMonitorsCommand.Execute(null);
        vm.Monitors.Should().BeSameAs(before);

        monitors.Monitors.Add(M(@"\\.\DISPLAY3", false, 4480, 1920, 1080, 1.0));
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        vm.RefreshMonitorsCommand.Execute(null);

        vm.Monitors.Should().HaveCount(4);
        names.Should().Contain(nameof(PositionPageViewModel.Monitors)).And.Contain(nameof(PositionPageViewModel.Map));
    }

    [Fact]
    public void The_map_follows_the_draft()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.Edge = ScreenEdge.Left;

        vm.Map.PillMarker!.Value.X.Should().BeApproximately(vm.Map.Tiles[0].Rect.X, 1e-6);
    }
}
