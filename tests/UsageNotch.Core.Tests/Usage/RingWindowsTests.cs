using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class RingWindowsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static UsageSnapshot Snap(params (string Id, double Used)[] windows) =>
        new(SnapshotStatus.Ok,
            windows.Select(w => new LimitWindow(w.Id, w.Id, w.Used, Now.AddHours(1))).ToList(),
            Now, "", null);

    [Fact]
    public void The_three_groups_are_ordered_outer_to_inner()
    {
        RingWindows.Claude.Should().HaveCount(3);
        RingWindows.Claude[0].Should().Equal("session", "five_hour");
        RingWindows.Claude[1].Should().Equal("weekly_all", "seven_day", "weekly");
        RingWindows.Claude[2].Should().Equal("weekly_scoped", "weekly_opus", "seven_day_opus");
    }

    [Fact]
    public void An_alias_group_finds_its_window_whichever_name_the_api_used()
    {
        Snap(("five_hour", 0.3)).Window(RingWindows.Claude[0])!.UsedFraction.Should().Be(0.3);
        Snap(("seven_day", 0.4)).Window(RingWindows.Claude[1])!.UsedFraction.Should().Be(0.4);
        Snap(("weekly_opus", 0.5)).Window(RingWindows.Claude[2])!.UsedFraction.Should().Be(0.5);
        Snap(("weekly_scoped", 0.6)).Window(RingWindows.Claude[2])!.UsedFraction.Should().Be(0.6);
    }

    [Fact]
    public void The_first_alias_of_the_group_wins_when_several_are_present()
    {
        var s = Snap(("weekly_opus", 0.2), ("weekly_scoped", 0.9));
        s.Window(RingWindows.Claude[2])!.UsedFraction.Should().Be(0.9);
    }

    [Fact]
    public void A_group_with_no_match_gives_null()
    {
        Snap(("session", 0.3)).Window(RingWindows.Claude[2]).Should().BeNull();
    }

    [Fact]
    public void A_group_never_borrows_another_rings_window()
    {
        Snap(("weekly_all", 0.7)).Window(RingWindows.Claude[0]).Should().BeNull();
    }
}
