using FluentAssertions;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Tests.Pill;

public class PillPresenterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Theme Theme = UsageNotch.Core.Settings.Theme.Codenotch;

    private static UsageSnapshot Snap(
        SnapshotStatus status,
        double? session,
        double? weeklyAll = null,
        double? weeklyScoped = null,
        DateTimeOffset? fetched = null,
        string note = "",
        string scopedId = "weekly_scoped")
    {
        var windows = new List<LimitWindow>();
        if (session is { } s) windows.Add(new LimitWindow("session", "Session en cours", s, Now.AddHours(2)));
        if (weeklyAll is { } a) windows.Add(new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", a, Now.AddDays(3)));
        if (weeklyScoped is { } p) windows.Add(new LimitWindow(scopedId, "Hebdomadaire (par modèle)", p, Now.AddDays(3)));
        return new UsageSnapshot(status, windows, fetched ?? Now, note, null);
    }

    private static CellModel Cell(
        UsageSnapshot s,
        SessionState agg = SessionState.Idle,
        CellContent content = CellContent.RingAndPercent,
        RingColoring coloring = RingColoring.PerRing) =>
        PillPresenter.Cell(s, RingWindows.Claude, agg, Theme, content, coloring, Now);

    [Fact]
    public void An_ok_reading_shows_the_session_fraction_percent_and_colour()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73));
        c.RingFraction.Should().BeApproximately(0.73, 1e-9);
        c.PercentText.Should().Be("73" + FrenchText.Nbsp + "%");
        c.RingColor.Should().Be(Theme.RingSession);
        c.TrackColor.Should().Be(Theme.RingTrack);
        c.TextColor.Should().Be(Theme.Text);
        c.Dimmed.Should().BeFalse();
        c.Exhausted.Should().BeFalse();
        c.BandColor.Should().Be(Theme.RingSession);
    }

    [Theory]
    [InlineData(0.10, "#28E07B")]
    [InlineData(0.85, "#FF4500")]
    public void The_ring_colour_follows_the_theme_thresholds(double used, string colour) =>
        Cell(Snap(SnapshotStatus.Ok, used), coloring: RingColoring.ByLevel).RingColor.Should().Be(colour);

    [Fact]
    public void A_full_window_is_exhausted()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 1.0));
        c.Exhausted.Should().BeTrue();
        c.RingFraction.Should().Be(1.0);
    }

    [Fact]
    public void A_missing_headline_window_is_a_dash_not_another_window()
    {
        var s = new UsageSnapshot(SnapshotStatus.Ok,
            [new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", 0.6, Now.AddDays(3))], Now, "", null);
        var c = Cell(s);
        c.RingFraction.Should().BeNull();
        c.PercentText.Should().Be("—");
        c.RingColor.Should().Be(Theme.RingTrack);
        c.BandColor.Should().Be(Theme.RingTrack);
    }

    [Fact]
    public void The_empty_startup_snapshot_waits_with_an_ellipsis()
    {
        var c = Cell(UsageSnapshot.Empty);
        PillPresenter.IsWaitingForFirstReading(UsageSnapshot.Empty).Should().BeTrue();
        c.RingFraction.Should().BeNull();
        c.PercentText.Should().Be("…");
    }

    [Fact]
    public void Needs_auth_shows_a_dash_even_with_old_windows()
    {
        var c = Cell(Snap(SnapshotStatus.NeedsAuth, 0.4, note: "Identifiant refusé"));
        c.RingFraction.Should().BeNull();
        c.PercentText.Should().Be("—");
    }

    [Fact]
    public void A_stale_status_dims_the_cell()
    {
        Cell(Snap(SnapshotStatus.Stale, 0.4)).Dimmed.Should().BeTrue();
    }

    [Fact]
    public void An_ok_reading_older_than_ten_minutes_is_dimmed()
    {
        Cell(Snap(SnapshotStatus.Ok, 0.4, fetched: Now.AddMinutes(-9))).Dimmed.Should().BeFalse();
        Cell(Snap(SnapshotStatus.Ok, 0.4, fetched: Now.AddMinutes(-11))).Dimmed.Should().BeTrue();
    }

    [Theory]
    [InlineData(SessionState.Idle, ActivityKind.None)]
    [InlineData(SessionState.Done, ActivityKind.Done)]
    [InlineData(SessionState.Running, ActivityKind.Running)]
    [InlineData(SessionState.Attention, ActivityKind.Attention)]
    public void The_aggregate_session_state_maps_to_an_activity(SessionState state, ActivityKind kind) =>
        Cell(Snap(SnapshotStatus.Ok, 0.2), state).Activity.Should().Be(kind);

    [Fact]
    public void Activity_colours_come_from_the_theme()
    {
        PillPresenter.ActivityColor(ActivityKind.Running, Theme).Should().Be(Theme.Running);
        PillPresenter.ActivityColor(ActivityKind.Attention, Theme).Should().Be(Theme.Attention);
        PillPresenter.ActivityColor(ActivityKind.Done, Theme).Should().Be(Theme.Done);
        PillPresenter.ActivityColor(ActivityKind.None, Theme).Should().Be(Theme.RingTrack);
    }

    [Fact]
    public void Attention_turns_the_folded_band_amber()
    {
        Cell(Snap(SnapshotStatus.Ok, 0.2), SessionState.Attention).BandColor.Should().Be(Theme.Attention);
    }

    [Theory]
    [InlineData(CellContent.RingAndPercent, true, true)]
    [InlineData(CellContent.RingOnly, true, false)]
    [InlineData(CellContent.PercentOnly, false, true)]
    public void Cell_content_setting_controls_what_is_shown(CellContent content, bool ring, bool percent)
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.2), content: content);
        c.ShowRing.Should().Be(ring);
        c.ShowPercent.Should().Be(percent);
    }

    [Fact]
    public void Window_length_includes_both_fillets() =>
        PillMetrics.WindowLength.Should().Be(PillMetrics.BodyLength + 2 * PillMetrics.Fillet);

    [Fact]
    public void The_three_rings_are_ordered_session_then_weekly_then_scoped()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73, 0.21, 0.52));

        c.Rings.Should().HaveCount(3);
        c.Rings[0].Fraction.Should().BeApproximately(0.73, 1e-9);
        c.Rings[1].Fraction.Should().BeApproximately(0.21, 1e-9);
        c.Rings[2].Fraction.Should().BeApproximately(0.52, 1e-9);
        c.Rings.Should().OnlyContain(r => r.TrackColor == Theme.RingTrack);
    }

    [Fact]
    public void A_legacy_opus_window_still_feeds_the_inner_ring()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.1, 0.2, 0.52, scopedId: "weekly_opus"));

        c.Rings[2].Fraction.Should().BeApproximately(0.52, 1e-9);
    }

    [Fact]
    public void Per_ring_colouring_gives_each_ring_its_own_theme_colour()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.95, 0.05, 0.5));

        c.Rings[0].Color.Should().Be(Theme.RingSession);
        c.Rings[1].Color.Should().Be(Theme.RingWeeklyAll);
        c.Rings[2].Color.Should().Be(Theme.RingWeeklyScoped);
    }

    [Fact]
    public void By_level_colouring_grades_each_ring_on_its_own_fraction()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.95, 0.05, 0.6), coloring: RingColoring.ByLevel);

        c.Rings[0].Color.Should().Be(Theme.LevelCritical);
        c.Rings[1].Color.Should().Be(Theme.LevelAmple);
        c.Rings[2].Color.Should().Be(Theme.LevelWatch);
    }

    [Fact]
    public void A_missing_window_draws_its_track_alone_without_moving_the_others()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73, weeklyScoped: 0.52));

        c.Rings.Should().HaveCount(3);
        c.Rings[1].Fraction.Should().BeNull();
        c.Rings[1].Color.Should().Be(Theme.RingTrack);
        c.Rings[2].Fraction.Should().BeApproximately(0.52, 1e-9);
    }

    [Fact]
    public void Needs_auth_leaves_the_three_rings_as_bare_tracks()
    {
        var c = Cell(Snap(SnapshotStatus.NeedsAuth, 0.4, 0.4, 0.4, note: "Identifiant refusé"));

        c.Rings.Should().OnlyContain(r => r.Fraction == null && r.Color == Theme.RingTrack);
        c.PercentText.Should().Be("—");
    }

    [Fact]
    public void Exhausted_and_the_percent_still_speak_for_the_session_alone()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 1.0, 0.1, 0.1));

        c.Exhausted.Should().BeTrue();
        c.PercentText.Should().Be("100" + FrenchText.Nbsp + "%");

        Cell(Snap(SnapshotStatus.Ok, 0.1, 1.0, 1.0)).Exhausted.Should().BeFalse();
    }
}
