using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Sessions;

public class SessionStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static HookEvent Ev(string kind, string id = "abcd1234-session", string cwd = @"C:\src\myproj",
        string prompt = "", string message = "", string tool = "", string cmd = "", string model = "", int ppid = 0) =>
        new(kind, id, ppid, cwd, prompt, message, tool, cmd, model);

    private static (SessionStore Store, FakeTimeProvider Time) Build()
    {
        var time = new FakeTimeProvider(Now);
        return (new SessionStore(time), time);
    }

    [Fact]
    public void Session_start_creates_an_idle_session_with_a_title_from_cwd()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.SessionStart)).Should().BeTrue();
        var s = store.Snapshot().Single();
        s.State.Should().Be(SessionState.Idle);
        s.Title.Should().Be("myproj · abcd");
        store.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void Running_records_prompt_and_start_time()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, prompt: "corrige le bug"));
        var s = store.Snapshot().Single();
        s.State.Should().Be(SessionState.Running);
        s.Prompt.Should().Be("corrige le bug");
        s.Started.Should().Be(Now);
        store.HasActiveSession.Should().BeTrue();
    }

    [Fact]
    public void Running_with_a_tool_records_the_last_action()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, tool: "Bash", cmd: "dotnet test"));
        store.Snapshot().Single().LastAction.Should().Be("🔧 Bash : dotnet test");
        store.Apply(Ev(HookEvent.Running, tool: "Read"));
        store.Snapshot().Single().LastAction.Should().Be("🔧 Read");
    }

    [Fact]
    public void Long_texts_are_truncated_with_an_ellipsis()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, prompt: new string('a', 200), tool: "Bash", cmd: new string('b', 100)));
        var s = store.Snapshot().Single();
        s.Prompt.Should().HaveLength(121).And.EndWith("…");
        s.LastAction.Should().EndWith("…");
        s.LastAction.Length.Should().Be("🔧 Bash : ".Length + 61);
    }

    [Fact]
    public void Attention_records_the_message_and_outranks_running()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, id: "one"));
        store.Apply(Ev(HookEvent.Attention, id: "two", message: "Autoriser Bash ?"));
        store.Snapshot()[0].Id.Should().Be("two");
        store.Snapshot()[0].AttentionMessage.Should().Be("Autoriser Bash ?");
        store.Aggregate.Should().Be(SessionState.Attention);
    }

    [Fact]
    public void Running_after_attention_clears_the_message_and_restarts_the_clock()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running));
        time.Advance(TimeSpan.FromMinutes(1));
        store.Apply(Ev(HookEvent.Attention, message: "?"));
        time.Advance(TimeSpan.FromMinutes(1));
        store.Apply(Ev(HookEvent.Running));
        var s = store.Snapshot().Single();
        s.AttentionMessage.Should().BeEmpty();
        s.Started.Should().Be(Now.AddMinutes(2), "attention → running est un nouveau tour");
    }

    [Fact]
    public void Done_freezes_the_total_and_persists_until_the_next_prompt()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running));
        time.Advance(TimeSpan.FromMinutes(3));
        store.Apply(Ev(HookEvent.Done));
        var s = store.Snapshot().Single();
        s.State.Should().Be(SessionState.Done);
        s.Total.Should().Be(TimeSpan.FromMinutes(3));
        store.HasActiveSession.Should().BeFalse();

        time.Advance(TimeSpan.FromMinutes(20));
        store.Sweep();
        store.Snapshot().Should().ContainSingle(x => x.State == SessionState.Done);

        store.Apply(Ev(HookEvent.Running));
        store.Snapshot().Single().State.Should().Be(SessionState.Running);
    }

    [Fact]
    public void Session_end_removes_the_session()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running));
        store.Apply(Ev(HookEvent.SessionEnd)).Should().BeTrue();
        store.Snapshot().Should().BeEmpty();
        store.Apply(Ev(HookEvent.SessionEnd)).Should().BeFalse("déjà retirée");
    }

    [Fact]
    public void Dismiss_removes_a_session()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Done));
        store.Dismiss("abcd1234-session").Should().BeTrue();
        store.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Changed_is_raised_only_on_a_visible_change()
    {
        var (store, _) = Build();
        var raised = 0;
        store.Changed += () => raised++;

        store.Apply(Ev(HookEvent.Running, tool: "Bash", cmd: "ls")).Should().BeTrue();
        store.Apply(Ev(HookEvent.Running, tool: "Bash", cmd: "ls")).Should().BeFalse();
        raised.Should().Be(1);
    }

    [Fact]
    public void Ppid_model_and_cwd_are_remembered_when_provided()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.SessionStart, cwd: "", ppid: 4242));
        store.Apply(Ev(HookEvent.Running, cwd: @"D:\work\api", model: "claude-opus-5", ppid: 0));
        var s = store.Snapshot().Single();
        s.ParentPid.Should().Be(4242);
        s.Model.Should().Be("claude-opus-5");
        s.Title.Should().Be("api · abcd");
        store.ParentPidOf("abcd1234-session").Should().Be(4242);
        store.ParentPidOf("missing").Should().BeNull();
    }

    [Fact]
    public void Sweep_turns_a_silent_running_session_idle_after_30_minutes()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running));
        time.Advance(TimeSpan.FromMinutes(31));
        store.Sweep().Should().BeTrue();
        store.Snapshot().Single().State.Should().Be(SessionState.Idle);
    }

    [Fact]
    public void Sweep_drops_idle_after_10_minutes_and_done_after_24_hours()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.SessionStart, id: "idle"));
        store.Apply(Ev(HookEvent.Done, id: "done"));

        time.Advance(TimeSpan.FromMinutes(11));
        store.Sweep().Should().BeTrue();
        store.Snapshot().Select(s => s.Id).Should().Equal("done");

        time.Advance(TimeSpan.FromHours(24));
        store.Sweep().Should().BeTrue();
        store.Snapshot().Should().BeEmpty();

        store.Sweep().Should().BeFalse("rien n'a changé");
    }

    [Fact]
    public void Snapshot_orders_by_state_then_most_recent_start()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running, id: "r-old"));
        time.Advance(TimeSpan.FromSeconds(1));
        store.Apply(Ev(HookEvent.Done, id: "d"));
        time.Advance(TimeSpan.FromSeconds(1));
        store.Apply(Ev(HookEvent.Running, id: "r-new"));
        time.Advance(TimeSpan.FromSeconds(1));
        store.Apply(Ev(HookEvent.Attention, id: "a"));

        store.Snapshot().Select(s => s.Id).Should().Equal("a", "r-new", "r-old", "d");
        store.Aggregate.Should().Be(SessionState.Attention);
    }

    [Fact]
    public void Aggregate_is_idle_when_there_is_nothing()
    {
        var (store, _) = Build();
        store.Aggregate.Should().Be(SessionState.Idle);
    }

    [Fact]
    public void Unknown_session_id_defaults_to_unknown_title()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, id: "unknown", cwd: ""));
        store.Snapshot().Single().Title.Should().Be("claude · unkn");
    }
}
