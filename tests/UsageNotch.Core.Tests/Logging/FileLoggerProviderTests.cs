using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Logging;

namespace UsageNotch.Core.Tests.Logging;

public class FileLoggerProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 34, 56, 789, TimeSpan.Zero);

    [Fact]
    public void Writes_a_formatted_line_into_the_daily_file()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Information);

        provider.CreateLogger("UsageNotch.Test").LogWarning("Port {Port} indisponible", 48666);

        var file = Path.Combine(dir.Path, "usagenotch-20260914.log");
        File.ReadAllLines(file).Should().Equal("2026-09-14T12:34:56.789Z [WRN] UsageNotch.Test: Port 48666 indisponible");
    }

    [Fact]
    public void The_minimum_level_is_read_on_every_call()
    {
        using var dir = new TempDir();
        var level = LogLevel.Information;
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => level);
        var logger = provider.CreateLogger("C");

        logger.LogDebug("caché");
        level = LogLevel.Debug;
        logger.LogDebug("visible");

        File.ReadAllText(Path.Combine(dir.Path, FileLoggerProvider.FileNameFor(Now))).Should().NotContain("caché").And.Contain("[DBG] C: visible");
    }

    [Fact]
    public void An_exception_is_appended_after_the_message()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Trace);

        provider.CreateLogger("C").LogError(new InvalidOperationException("boum"), "Échec");

        var text = File.ReadAllText(Path.Combine(dir.Path, FileLoggerProvider.FileNameFor(Now)));
        text.Should().Contain("[ERR] C: Échec").And.Contain("System.InvalidOperationException: boum");
    }

    [Fact]
    public void Old_log_files_are_purged_and_other_files_kept()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "usagenotch-20260906.log"), "8 jours");
        File.WriteAllText(Path.Combine(dir.Path, "usagenotch-20260908.log"), "6 jours");
        File.WriteAllText(Path.Combine(dir.Path, "usagenotch-garbage.log"), "nom illisible");
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"), "{}");

        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Information);

        Directory.GetFiles(dir.Path).Select(Path.GetFileName).Should().BeEquivalentTo(
            "usagenotch-20260908.log", "usagenotch-garbage.log", "settings.json");
    }

    [Fact]
    public void Concurrent_writes_keep_every_line_whole()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Information);
        var logger = provider.CreateLogger("C");

        Parallel.For(0, 50, i => logger.LogInformation("ligne {Index}", i));

        var lines = File.ReadAllLines(Path.Combine(dir.Path, FileLoggerProvider.FileNameFor(Now)));
        lines.Should().HaveCount(50).And.OnlyContain(l => l.StartsWith("2026-09-14T12:34:56.789Z [INF] C: ligne "));
    }

    [Fact]
    public void None_is_never_enabled()
    {
        using var dir = new TempDir();
        using var provider = new FileLoggerProvider(dir.Path, new FakeTimeProvider(Now), () => LogLevel.Trace);
        provider.CreateLogger("C").IsEnabled(LogLevel.None).Should().BeFalse();
    }
}
