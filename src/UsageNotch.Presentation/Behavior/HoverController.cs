namespace UsageNotch.Presentation.Behavior;

/// <summary>
/// Décide si la carte est visible, si la pilule est dépliée et si la carte est verrouillée, à partir des entrées et
/// sorties du pointeur, du verrou et des aperçus. Thread-safe. <see cref="Changed"/> peut être levé sur un thread de minuterie.
/// </summary>
public sealed class HoverController(TimeProvider time) : IDisposable
{
    public static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan FoldDelay = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan PeekDuration = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private bool _onPill;
    private bool _onCard;
    private bool _peeking;
    private bool _disposed;
    private ITimer? _closeTimer;
    private ITimer? _foldTimer;
    private ITimer? _peekTimer;

    private bool _cardVisible;
    private bool _unfolded;
    private bool _locked;

    public event Action? Changed;

    public bool CardVisible { get { lock (_gate) return _cardVisible; } }
    public bool Unfolded { get { lock (_gate) return _unfolded; } }
    public bool Locked { get { lock (_gate) return _locked; } }

    public void PointerEnteredPill() => Mutate(() => { _onPill = true; OpenLocked(); });
    public void PointerLeftPill() => Mutate(() => { _onPill = false; ScheduleCloseLocked(); });
    public void PointerEnteredCard() => Mutate(() => { _onCard = true; OpenLocked(); });
    public void PointerLeftCard() => Mutate(() => { _onCard = false; ScheduleCloseLocked(); });

    public void ToggleLock() => Mutate(() =>
    {
        _locked = !_locked;
        if (_locked) OpenLocked();
        else ScheduleCloseLocked();
    });

    public void Peek() => Mutate(() =>
    {
        _peeking = true;
        OpenLocked();
        _peekTimer?.Dispose();
        _peekTimer = time.CreateTimer(_ => Mutate(() =>
        {
            _peeking = false;
            ScheduleCloseLocked();
        }), null, PeekDuration, Timeout.InfiniteTimeSpan);
    });

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            DisposeTimersLocked();
            _peekTimer?.Dispose();
            _peekTimer = null;
        }
    }

    private void OpenLocked()
    {
        DisposeTimersLocked();
        _cardVisible = true;
        _unfolded = true;
    }

    private void ScheduleCloseLocked()
    {
        if (_onPill || _onCard || _locked || _peeking) return;
        // Déjà programmé : une sortie répétée (filet de sécurité de l'App) ne doit pas repousser la fermeture.
        if (_closeTimer is not null || _foldTimer is not null) return;
        _closeTimer = time.CreateTimer(_ => Mutate(() =>
        {
            if (!IsIdleLocked()) return;
            _cardVisible = false;
        }), null, CloseDelay, Timeout.InfiniteTimeSpan);
        _foldTimer = time.CreateTimer(_ => Mutate(() =>
        {
            if (!IsIdleLocked()) return;
            _cardVisible = false;
            _unfolded = false;
        }), null, FoldDelay, Timeout.InfiniteTimeSpan);
    }

    private bool IsIdleLocked() => !(_onPill || _onCard || _locked || _peeking);

    private void DisposeTimersLocked()
    {
        _closeTimer?.Dispose();
        _foldTimer?.Dispose();
        _closeTimer = null;
        _foldTimer = null;
    }

    private void Mutate(Action change)
    {
        bool changed;
        lock (_gate)
        {
            if (_disposed) return;
            var before = (_cardVisible, _unfolded, _locked);
            change();
            changed = before != (_cardVisible, _unfolded, _locked);
        }
        if (changed) Changed?.Invoke();
    }
}
