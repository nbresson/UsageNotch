using FluentAssertions;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Card;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Tests.Card;

public class CardPresenterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Theme Theme = UsageNotch.Core.Settings.Theme.Codenotch;

    private static Session S(string id, SessionState state, string title = "proj · abcd", string action = "",
        string attention = "", string prompt = "", TimeSpan total = default, int startedMinutesAgo = 1) =>
        new(id, title, state, Now.AddMinutes(-startedMinutesAgo), total, action, attention, prompt, "", 0, "", Now);

    private static UsageSnapshot Ok(params LimitWindow[] windows) => new(SnapshotStatus.Ok, windows, Now, "", null);

    private static CardModel Build(UsageSnapshot s, params Session[] sessions) =>
        CardPresenter.Build(s, "Claude", sessions, Theme, Now, TimeZoneInfo.Utc);

    [Fact]
    public void Window_rows_show_label_reset_bar_and_used_text()
    {
        var card = Build(Ok(
            new LimitWindow("session", "Session en cours", 0.73, Now.AddMinutes(51)),
            new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", 0.07, new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero))));

        card.Title.Should().Be("Claude");
        card.Subtitle.Should().BeNull();
        card.Note.Should().BeNull();
        card.Windows.Should().HaveCount(2);
        card.Windows[0].Should().Be(new WindowRow("Session en cours", "Réinitialisation dans 51 min", 0.73, Theme.LevelWatch,
            "73" + FrenchText.Nbsp + "% utilisé"));
        card.Windows[1].ResetText.Should().Be("Réinitialisation jeu. 00:00");
        card.Windows[1].BarColor.Should().Be(Theme.LevelAmple);
    }

    [Fact]
    public void A_stale_snapshot_gets_an_updated_ago_subtitle_and_keeps_its_note()
    {
        var s = new UsageSnapshot(SnapshotStatus.Stale,
            [new LimitWindow("session", "Session en cours", 0.4, Now.AddHours(2))], Now.AddMinutes(-12), "HTTP 500", null);
        var card = Build(s);
        card.Subtitle.Should().Be("Mis à jour il y a 12 min");
        card.Note.Should().Be("HTTP 500");
    }

    [Fact]
    public void Needs_auth_shows_only_the_note()
    {
        var s = new UsageSnapshot(SnapshotStatus.NeedsAuth,
            [new LimitWindow("session", "Session en cours", 0.4, Now.AddHours(2))], Now, "Identifiant refusé (changement de compte ?).", null);
        var card = Build(s);
        card.Windows.Should().BeEmpty();
        card.Note.Should().Be("Identifiant refusé (changement de compte ?).");
    }

    [Fact]
    public void The_startup_snapshot_says_it_is_waiting()
    {
        var card = Build(UsageSnapshot.Empty);
        card.Windows.Should().BeEmpty();
        card.Note.Should().Be("En attente de la première lecture…");
        card.Subtitle.Should().BeNull();
    }

    [Fact]
    public void Idle_sessions_are_not_listed_and_at_most_five_are()
    {
        var sessions = Enumerable.Range(0, 7).Select(i => S($"r{i}", SessionState.Running)).Append(S("idle", SessionState.Idle)).ToArray();
        var card = Build(Ok(), sessions);
        card.Sessions.Should().HaveCount(CardPresenter.MaxSessions);
        card.Sessions.Should().NotContain(r => r.SessionId == "idle");
    }

    [Fact]
    public void Session_details_depend_on_the_state()
    {
        var card = Build(Ok(),
            S("a", SessionState.Attention, attention: "Autoriser Bash ?"),
            S("a2", SessionState.Attention),
            S("r", SessionState.Running, action: "🔧 Bash : dotnet test", prompt: "fais X"),
            S("r2", SessionState.Running, prompt: "corrige le bug"),
            S("r3", SessionState.Running),
            S("d", SessionState.Done, total: TimeSpan.FromMinutes(3)));

        card.Sessions.Select(r => r.Detail).Should().Equal(
            "Autoriser Bash ?", "Attend votre réponse", "🔧 Bash : dotnet test", "corrige le bug", "En cours");
        card.Sessions.Select(r => r.Activity).Should().Equal(
            ActivityKind.Attention, ActivityKind.Attention, ActivityKind.Running, ActivityKind.Running, ActivityKind.Running);
        card.Sessions[0].DotColor.Should().Be(Theme.Attention);
        card.Sessions[2].DotColor.Should().Be(Theme.Running);
    }

    [Fact]
    public void A_done_session_shows_its_duration()
    {
        var card = Build(Ok(), S("d", SessionState.Done, title: "api · 1234", total: TimeSpan.FromSeconds(42)));
        card.Sessions.Should().ContainSingle().Which.Should().Be(
            new SessionRow("d", "api · 1234", "Terminé en 42 s", ActivityKind.Done, Theme.Done));
    }
}
