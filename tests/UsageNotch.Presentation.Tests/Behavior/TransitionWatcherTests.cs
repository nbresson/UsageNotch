using FluentAssertions;
using UsageNotch.Core.Sessions;
using UsageNotch.Presentation.Behavior;

namespace UsageNotch.Presentation.Tests.Behavior;

public class TransitionWatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static Session S(string id, SessionState state) =>
        new(id, $"proj · {id}", state, Now, TimeSpan.Zero, "", "", "", "", 0, "", Now);

    [Fact]
    public void A_session_seen_for_the_first_time_is_never_announced()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Done), S("b", SessionState.Attention)]).Should().BeEmpty();
    }

    [Fact]
    public void Running_to_done_is_announced_once()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running)]);

        w.Observe([S("a", SessionState.Done)]).Should().Equal(new SessionTransition("a", "proj · a", TransitionKind.Done));
        w.Observe([S("a", SessionState.Done)]).Should().BeEmpty();
    }

    [Fact]
    public void Running_to_attention_is_announced_and_back_to_running_is_not()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running)]);

        w.Observe([S("a", SessionState.Attention)]).Should().ContainSingle().Which.Kind.Should().Be(TransitionKind.Attention);
        w.Observe([S("a", SessionState.Running)]).Should().BeEmpty();
    }

    [Fact]
    public void Idle_to_running_is_not_announced()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Idle)]);
        w.Observe([S("a", SessionState.Running)]).Should().BeEmpty();
    }

    [Fact]
    public void Several_sessions_are_reported_in_input_order()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running), S("b", SessionState.Running)]);

        w.Observe([S("b", SessionState.Attention), S("a", SessionState.Done)])
            .Select(t => t.SessionId).Should().Equal("b", "a");
    }

    [Fact]
    public void A_removed_session_is_forgotten_and_counts_as_new_if_it_returns()
    {
        var w = new TransitionWatcher();
        w.Observe([S("a", SessionState.Running)]);
        w.Observe([]);
        w.Observe([S("a", SessionState.Done)]).Should().BeEmpty();
    }
}
