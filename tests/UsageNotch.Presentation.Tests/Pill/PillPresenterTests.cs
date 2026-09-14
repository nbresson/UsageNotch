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

    private static UsageSnapshot Snap(SnapshotStatus status, double? session, DateTimeOffset? fetched = null, string note = "")
    {
        var windows = session is { } f
            ? new[] { new LimitWindow("session", "Session en cours", f, Now.AddHours(2)) }
            : Array.Empty<LimitWindow>();
        return new UsageSnapshot(status, windows, fetched ?? Now, note, null);
    }

    private static CellModel Cell(UsageSnapshot s, SessionState agg = SessionState.Idle, CellContent content = CellContent.RingAndPercent) =>
        PillPresenter.Cell(s, "session", agg, Theme, content, Now);

    [Fact]
    public void An_ok_reading_shows_the_headline_fraction_percent_and_level_colour()
    {
        var c = Cell(Snap(SnapshotStatus.Ok, 0.73));
        c.RingFraction.Should().BeApproximately(0.73, 1e-9);
        c.PercentText.Should().Be("73" + FrenchText.Nbsp + "%");
        c.RingColor.Should().Be(Theme.LevelWatch);
        c.TrackColor.Should().Be(Theme.RingTrack);
        c.TextColor.Should().Be(Theme.Text);
        c.Dimmed.Should().BeFalse();
        c.Exhausted.Should().BeFalse();
        c.BandColor.Should().Be(Theme.LevelWatch);
    }

    [Theory]
    [InlineData(0.10, "#28E07B")]
    [InlineData(0.85, "#FF4500")]
    public void The_ring_colour_follows_the_theme_thresholds(double used, string colour) =>
        Cell(Snap(SnapshotStatus.Ok, used)).RingColor.Should().Be(colour);

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
}
