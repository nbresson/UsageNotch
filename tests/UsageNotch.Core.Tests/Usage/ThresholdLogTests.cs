using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ThresholdLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static ThresholdLog Build(TempDir dir, TimeProvider time) =>
        new(dir.File("notifications.json"), time, NullLogger<ThresholdLog>.Instance);

    [Fact]
    public void A_recorded_alert_survives_a_reload()
    {
        using var dir = new TempDir();
        var time = new FakeTimeProvider(Now);
        var log = Build(dir, time);
        log.Load();

        log.Add("session|Warning|42", Now.AddHours(3));

        log.Contains("session|Warning|42").Should().BeTrue();
        var reloaded = Build(dir, time);
        reloaded.Load();
        reloaded.Contains("session|Warning|42").Should().BeTrue();
        reloaded.Contains("session|Exhausted|42").Should().BeFalse();
    }

    [Fact]
    public void Expired_alerts_are_forgotten_on_load()
    {
        using var dir = new TempDir();
        var time = new FakeTimeProvider(Now);
        var log = Build(dir, time);
        log.Load();
        log.Add("session|Warning|42", Now.AddHours(1));
        log.Add("weekly_all|Warning|7", Now.AddDays(3));

        time.Advance(TimeSpan.FromHours(2));
        var reloaded = Build(dir, time);
        reloaded.Load();

        reloaded.Contains("session|Warning|42").Should().BeFalse();
        reloaded.Contains("weekly_all|Warning|7").Should().BeTrue();
    }

    [Fact]
    public void A_missing_or_unreadable_file_starts_empty_without_throwing()
    {
        using var dir = new TempDir();
        var time = new FakeTimeProvider(Now);
        var missing = Build(dir, time);
        missing.Load();
        missing.Contains("session|Warning|42").Should().BeFalse();

        File.WriteAllText(dir.File("notifications.json"), "{ pas du json");
        var corrupt = Build(dir, time);
        corrupt.Load();
        corrupt.Contains("session|Warning|42").Should().BeFalse();
        corrupt.Add("session|Warning|42", Now.AddHours(1));
        corrupt.Contains("session|Warning|42").Should().BeTrue();
    }

    [Fact]
    public void Adding_the_same_alert_twice_keeps_one_entry()
    {
        using var dir = new TempDir();
        var time = new FakeTimeProvider(Now);
        var log = Build(dir, time);
        log.Load();

        log.Add("session|Warning|42", Now.AddHours(1));
        log.Add("session|Warning|42", Now.AddHours(1));

        File.ReadAllText(dir.File("notifications.json")).Split("session|Warning|42").Length.Should().Be(2);
    }
}
