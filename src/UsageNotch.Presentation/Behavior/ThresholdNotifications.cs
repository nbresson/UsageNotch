using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Behavior;

/// <summary>
/// Envoie une notification quand une lecture d'usage franchit le seuil d'alerte ou 100 %, si les réglages le permettent.
/// Les lectures arrivent sur un thread d'arrière-plan et sont relues sur le thread UI. À démarrer avant host.StartAsync.
/// </summary>
public sealed class ThresholdNotifications : IDisposable
{
    private readonly UsageStore _usage;
    private readonly SettingsStore _settings;
    private readonly ThresholdLog _log;
    private readonly IUserNotifier _notifier;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;
    private readonly ThresholdWatcher _watcher;
    private readonly Action<UsageSnapshot> _onUsage;
    private bool _started;
    private bool _disposed;

    public ThresholdNotifications(
        UsageStore usage,
        SettingsStore settings,
        ThresholdLog log,
        IUserNotifier notifier,
        IUiDispatcher ui,
        TimeProvider time,
        TimeZoneInfo zone)
    {
        _usage = usage;
        _settings = settings;
        _log = log;
        _notifier = notifier;
        _ui = ui;
        _time = time;
        _zone = zone;
        _watcher = new ThresholdWatcher(log);
        _onUsage = _ => _ui.Post(Check);
    }

    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        _log.Load();
        _usage.Changed += _onUsage;
        Check();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _usage.Changed -= _onUsage;
    }

    private void Check()
    {
        if (_disposed) return;
        var settings = _settings.Current;
        if (!settings.ThresholdNotifications) return;

        foreach (var alert in _watcher.Observe(_usage.Current, settings.NotifyThreshold, _time.GetUtcNow(), _zone))
        {
            _notifier.Show(alert.Title, alert.Message, alert.Level == ThresholdLevel.Exhausted);
        }
    }
}
