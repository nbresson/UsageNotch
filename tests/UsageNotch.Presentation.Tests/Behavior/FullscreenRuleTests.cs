using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class FullscreenRuleTests
{
    private static readonly PixelRect Monitor = new(0, 0, 2560, 1080);

    private static ForegroundWindow Borderless(PixelRect bounds) =>
        new(bounds, HasCaption: false, IsMinimized: false, IsVisible: true, IsOwnProcess: false, ClassName: "UnityWndClass");

    [Fact]
    public void A_borderless_window_covering_the_monitor_is_fullscreen()
    {
        FullscreenRule.IsFullscreenOn(Borderless(Monitor), Monitor).Should().BeTrue();
    }

    [Fact]
    public void A_window_slightly_larger_than_the_monitor_is_fullscreen()
    {
        FullscreenRule.IsFullscreenOn(Borderless(new PixelRect(-8, -8, 2576, 1096)), Monitor).Should().BeTrue();
    }

    [Fact]
    public void A_maximized_window_with_a_title_bar_is_not_fullscreen()
    {
        var maximized = Borderless(new PixelRect(-8, -8, 2576, 1096)) with { HasCaption = true };

        FullscreenRule.IsFullscreenOn(maximized, Monitor).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 0, 2560, 1032)]
    [InlineData(10, 0, 2550, 1080)]
    [InlineData(2560, 0, 1920, 1080)]
    public void A_window_that_does_not_cover_the_whole_monitor_is_not_fullscreen(int x, int y, int w, int h)
    {
        FullscreenRule.IsFullscreenOn(Borderless(new PixelRect(x, y, w, h)), Monitor).Should().BeFalse();
    }

    [Fact]
    public void Minimized_invisible_and_own_windows_are_ignored()
    {
        FullscreenRule.IsFullscreenOn(Borderless(Monitor) with { IsMinimized = true }, Monitor).Should().BeFalse();
        FullscreenRule.IsFullscreenOn(Borderless(Monitor) with { IsVisible = false }, Monitor).Should().BeFalse();
        FullscreenRule.IsFullscreenOn(Borderless(Monitor) with { IsOwnProcess = true }, Monitor).Should().BeFalse();
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void The_desktop_and_the_taskbar_are_never_fullscreen(string className)
    {
        FullscreenRule.IsFullscreenOn(Borderless(Monitor) with { ClassName = className }, Monitor).Should().BeFalse();
    }

    [Fact]
    public void No_foreground_window_is_not_fullscreen()
    {
        FullscreenRule.IsFullscreenOn(null, Monitor).Should().BeFalse();
    }
}
