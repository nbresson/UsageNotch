using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests.Behavior;

public sealed class ThresholdNotificationsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeNotifier : IUserNotifier
    {
        public List<(string Title, string Message, bool Critical)> Shown { get; } = [];
        public void Show(string title, string message, bool critical) => Shown.Add((title, message, critical));
    }

    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly UsageStore _usage;
    private readonly SettingsStore _settings;
    private readonly FakeNotifier _notifier = new();
    private readonly ThresholdNotifications _notifications;

    public ThresholdNotificationsTests()
    {
        _usage = new UsageStore(_dir.File("usage.json"), _time, NullLogger<UsageStore>.Instance);
        _settings = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _settings.Load();
        var log = new ThresholdLog(_dir.File("notifications.json"), _time, NullLogger<ThresholdLog>.Instance);
        _notifications = new ThresholdNotifications(_usage, _settings, log, _notifier, new ImmediateDispatcher(), _time, TimeZoneInfo.Utc);
    }

    public void Dispose()
    {
        _notifications.Dispose();
        _dir.Dispose();
    }

    private void Read(double session) =>
        _usage.Apply(new FetchResult.Success([new LimitWindow("session", "Session en cours", session, Now.AddMinutes(51))]));

    [Fact]
    public void A_reading_over_the_threshold_shows_a_notification()
    {
        _notifications.Start();

        Read(0.5);
        Read(0.85);

        _notifier.Shown.Should().ContainSingle();
        _notifier.Shown[0].Title.Should().StartWith("Session en cours : 85");
        _notifier.Shown[0].Message.Should().Be("Réinitialisation dans 51 min");
        _notifier.Shown[0].Critical.Should().BeFalse();
    }

    [Fact]
    public void Reaching_the_limit_is_critical()
    {
        _notifications.Start();

        Read(1.0);

        _notifier.Shown.Should().ContainSingle().Which.Critical.Should().BeTrue();
    }

    [Fact]
    public void The_threshold_comes_from_the_settings()
    {
        _settings.Save(_settings.Current with { NotifyThreshold = 0.9 });
        _notifications.Start();

        Read(0.85);
        _notifier.Shown.Should().BeEmpty();

        Read(0.92);
        _notifier.Shown.Should().ContainSingle();
    }

    [Fact]
    public void Disabled_notifications_show_nothing_and_remember_nothing()
    {
        _settings.Save(_settings.Current with { ThresholdNotifications = false });
        _notifications.Start();

        Read(0.85);
        _notifier.Shown.Should().BeEmpty();

        _settings.Save(_settings.Current with { ThresholdNotifications = true });
        Read(0.86);
        _notifier.Shown.Should().ContainSingle();
    }

    [Fact]
    public void Start_checks_the_reading_already_loaded()
    {
        Read(0.9);

        _notifications.Start();

        _notifier.Shown.Should().ContainSingle();
    }

    [Fact]
    public void Nothing_is_shown_after_dispose()
    {
        _notifications.Start();
        _notifications.Dispose();

        Read(0.95);

        _notifier.Shown.Should().BeEmpty();
    }
}
