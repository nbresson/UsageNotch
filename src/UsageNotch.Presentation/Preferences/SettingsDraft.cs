using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

/// <summary>
/// Réglages en cours d'édition dans la fenêtre de réglages. Chaque modification est une fonction appliquée aux réglages
/// enregistrés : <see cref="Value"/> change aussitôt (aperçu) et l'enregistrement suit au plus tard <see cref="CommitDelay"/>
/// après la première modification non enregistrée. Une modification enregistrée ailleurs (glisser Alt, settings.json édité
/// à la main, Quitter) n'est jamais écrasée : les modifications en attente sont rejouées par-dessus.
/// À utiliser depuis le thread UI.
/// </summary>
public sealed class SettingsDraft : IDisposable
{
    public static readonly TimeSpan CommitDelay = TimeSpan.FromMilliseconds(250);

    private readonly SettingsStore _store;
    private readonly IUiDispatcher _ui;
    private readonly ITimer _timer;
    private readonly Action<CoreSettings> _onStoreChanged;
    private Func<CoreSettings, CoreSettings>? _pending;
    private bool _scheduled;
    private bool _disposed;

    public SettingsDraft(SettingsStore store, IUiDispatcher ui, TimeProvider time)
    {
        _store = store;
        _ui = ui;
        Value = store.Current;
        _timer = time.CreateTimer(_ => _ui.Post(OnTimer), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _onStoreChanged = _ => _ui.Post(Recompute);
        _store.Changed += _onStoreChanged;
    }

    /// <summary>Les réglages tels qu'ils seront enregistrés, bornes appliquées.</summary>
    public CoreSettings Value { get; private set; }

    public bool HasPendingEdits => _pending is not null;

    /// <summary>Levé sur le thread UI quand <see cref="Value"/> change, que la modification vienne d'ici ou d'ailleurs.</summary>
    public event Action? Changed;

    public void Edit(Func<CoreSettings, CoreSettings> edit)
    {
        if (_disposed) return;

        var previous = _pending;
        _pending = previous is null ? edit : s => edit(previous(s));
        Recompute();

        if (_scheduled) return;
        _scheduled = true;
        _timer.Change(CommitDelay, Timeout.InfiniteTimeSpan);
    }

    public void Flush()
    {
        if (_pending is null) return;

        var pending = _pending;
        _pending = null;
        _scheduled = false;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _store.Save(pending(_store.Current));
        Recompute();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Flush();
        _disposed = true;
        _store.Changed -= _onStoreChanged;
        _timer.Dispose();
    }

    private void OnTimer()
    {
        if (!_disposed) Flush();
    }

    private void Recompute()
    {
        if (_disposed) return;

        var next = _pending is null ? _store.Current : _pending(_store.Current).Clamp();
        if (next == Value) return;
        Value = next;
        Changed?.Invoke();
    }
}
