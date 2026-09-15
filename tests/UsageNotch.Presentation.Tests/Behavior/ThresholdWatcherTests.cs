using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Tests.Behavior;

public sealed class ThresholdWatcherTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SessionReset = Now.AddMinutes(51);

    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly ThresholdLog _log;
    private readonly ThresholdWatcher _watcher;

    public ThresholdWatcherTests()
    {
        _log = NewLog();
        _watcher = new ThresholdWatcher(_log);
    }

    public void Dispose() => _dir.Dispose();

    private ThresholdLog NewLog()
    {
        var log = new ThresholdLog(_dir.File("notifications.json"), _time, NullLogger<ThresholdLog>.Instance);
        log.Load();
        return log;
    }

    private static UsageSnapshot Ok(params LimitWindow[] windows) => new(SnapshotStatus.Ok, windows, Now, "", null);

    private static LimitWindow Session(double used, DateTimeOffset? resets = null) =>
        new("session", "Session en cours", used, resets ?? SessionReset);

    private static LimitWindow Opus(double used) => new("weekly_opus", "Hebdomadaire (Opus)", used, Now.AddDays(3));

    private IReadOnlyList<ThresholdAlert> Observe(UsageSnapshot snapshot, double threshold = 0.8) =>
        _watcher.Observe(snapshot, threshold, _time.GetUtcNow(), TimeZoneInfo.Utc);

    [Fact]
    public void Nothing_is_announced_below_the_threshold()
    {
        Observe(Ok(Session(0.79))).Should().BeEmpty();
    }

    [Fact]
    public void Crossing_the_threshold_is_announced_once_with_the_usage_and_reset_time()
    {
        var alerts = Observe(Ok(Session(0.81)));

        var alert = alerts.Should().ContainSingle().Subject;
        alert.WindowId.Should().Be("session");
        alert.Level.Should().Be(ThresholdLevel.Warning);
        alert.Title.Should().Be("Session en cours : 81" + FrenchText.Nbsp + "% utilisés");
        alert.Message.Should().Be("Réinitialisation dans 51 min");

        Observe(Ok(Session(0.85))).Should().BeEmpty();
    }

    [Fact]
    public void Reaching_the_limit_after_a_warning_is_announced_again()
    {
        Observe(Ok(Session(0.85)));

        var alert = Observe(Ok(Session(1.0))).Should().ContainSingle().Subject;

        alert.Level.Should().Be(ThresholdLevel.Exhausted);
        alert.Title.Should().Be("Session en cours : limite atteinte");
        Observe(Ok(Session(1.0))).Should().BeEmpty();
    }

    [Fact]
    public void Jumping_straight_to_the_limit_announces_only_the_limit()
    {
        Observe(Ok(Session(1.0))).Should().ContainSingle().Which.Level.Should().Be(ThresholdLevel.Exhausted);

        Observe(Ok(Session(0.9))).Should().BeEmpty();
    }

    [Fact]
    public void A_new_period_announces_again()
    {
        Observe(Ok(Session(0.85)));

        Observe(Ok(Session(0.85, SessionReset.AddHours(5)))).Should().ContainSingle();
    }

    [Theory]
    [InlineData(SnapshotStatus.Stale)]
    [InlineData(SnapshotStatus.Error)]
    [InlineData(SnapshotStatus.NeedsAuth)]
    [InlineData(SnapshotStatus.Backoff)]
    public void Only_fresh_readings_are_announced(SnapshotStatus status)
    {
        var snapshot = new UsageSnapshot(status, [Session(0.95)], Now, "", null);

        Observe(snapshot).Should().BeEmpty();
    }

    [Fact]
    public void A_window_whose_reset_time_has_passed_is_ignored()
    {
        Observe(Ok(Session(0.95, Now.AddMinutes(-1)))).Should().BeEmpty();
    }

    [Fact]
    public void Each_window_is_announced_on_its_own_in_reading_order()
    {
        var alerts = Observe(Ok(Session(0.9), Opus(1.0)));

        alerts.Select(a => (a.WindowId, a.Level)).Should().Equal(("session", ThresholdLevel.Warning), ("weekly_opus", ThresholdLevel.Exhausted));
    }

    [Fact]
    public void The_chosen_threshold_is_used()
    {
        Observe(Ok(Session(0.73)), threshold: 0.7).Should().ContainSingle();
    }

    [Fact]
    public void A_restart_does_not_repeat_an_alert()
    {
        Observe(Ok(Session(0.85)));

        var afterRestart = new ThresholdWatcher(NewLog());

        afterRestart.Observe(Ok(Session(0.85)), 0.8, Now, TimeZoneInfo.Utc).Should().BeEmpty();
    }
}
