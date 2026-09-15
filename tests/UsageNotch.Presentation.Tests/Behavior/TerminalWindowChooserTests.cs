using FluentAssertions;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class TerminalWindowChooserTests
{
    // claude(100) → pwsh(200) → WindowsTerminal(300) → explorer(400)
    private static readonly Dictionary<int, int> Parents = new()
    {
        [100] = 200,
        [200] = 300,
        [300] = 400,
        [400] = 0,
        [500] = 200, // conhost dont le parent est le shell
        [900] = 1,   // UsageNotch lui-même
    };

    [Fact]
    public void The_chain_starts_at_the_pid_and_stops_at_zero() =>
        TerminalWindowChooser.AncestorChain(100, Parents).Should().Equal(100, 200, 300, 400);

    [Fact]
    public void The_chain_survives_a_cycle_and_a_missing_parent()
    {
        var cyclic = new Dictionary<int, int> { [1] = 2, [2] = 1 };
        TerminalWindowChooser.AncestorChain(1, cyclic).Should().Equal(1, 2);
        TerminalWindowChooser.AncestorChain(42, cyclic).Should().Equal(42);
    }

    [Fact]
    public void The_chain_is_limited_to_eight_ancestors()
    {
        var deep = Enumerable.Range(1, 20).ToDictionary(i => i, i => i + 1);
        TerminalWindowChooser.AncestorChain(1, deep).Should().HaveCount(TerminalWindowChooser.MaxDepth + 1);
    }

    [Fact]
    public void The_nearest_ancestor_window_wins_over_explorer()
    {
        var windows = new[] { new TopLevelWindow(4, 400), new TopLevelWindow(3, 300) };
        TerminalWindowChooser.Choose(100, Parents, windows, selfPid: 900).Should().Be((nint)3);
    }

    [Fact]
    public void A_conhost_window_whose_parent_is_in_the_chain_is_found()
    {
        var windows = new[] { new TopLevelWindow(4, 400), new TopLevelWindow(5, 500) };
        TerminalWindowChooser.Choose(100, Parents, windows, selfPid: 900).Should().Be((nint)5);
    }

    [Fact]
    public void Our_own_windows_are_never_chosen()
    {
        var parents = new Dictionary<int, int>(Parents) { [900] = 200 };
        var windows = new[] { new TopLevelWindow(9, 900), new TopLevelWindow(4, 400) };
        TerminalWindowChooser.Choose(100, parents, windows, selfPid: 900).Should().Be((nint)4);
    }

    [Fact]
    public void No_related_window_gives_null()
    {
        var windows = new[] { new TopLevelWindow(7, 777) };
        TerminalWindowChooser.Choose(100, Parents, windows, selfPid: 900).Should().BeNull();
    }
}
