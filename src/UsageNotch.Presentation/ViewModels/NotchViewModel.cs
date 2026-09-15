using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Card;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Services;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.ViewModels;

/// <summary>
/// État observable de la pilule et de la carte. Toutes les notifications des magasins passent par
/// <see cref="IUiDispatcher"/> avant de relire l'état courant ; les propriétés ne changent que sur le thread UI.
/// </summary>
public sealed class NotchViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(30);

    private readonly UsageStore _usage;
    private readonly SessionStore _sessions;
    private readonly SettingsStore _settingsStore;
    private readonly IUsageProvider _provider;
    private readonly HoverController _hover;
    private readonly IUiDispatcher _ui;
    private readonly ISessionFocus _focus;
    private readonly ISoundPlayer _sound;
    private readonly IAccentColorSource _accent;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;
    private readonly TransitionWatcher _watcher = new();
    private readonly ITimer _clock;

    private readonly Action<UsageSnapshot> _onUsage;
    private readonly Action _onSessions;
    private readonly Action<CoreSettings> _onSettings;
    private readonly Action _onHover;
    private bool _disposed;

    private CellModel _cell = null!;
    private CardModel _card = null!;
    private Theme _theme = Theme.Codenotch;
    private CoreSettings _settings = new();
    private bool _cardVisible;
    private bool _unfolded;
    private bool _locked;
    private bool _foregroundFullscreen;
    private bool _fullscreenActive;
    private string _trayText = "";

    public NotchViewModel(
        UsageStore usage,
        SessionStore sessions,
        SettingsStore settings,
        IUsageProvider provider,
        Action requestRefresh,
        HoverController hover,
        IUiDispatcher ui,
        ISessionFocus focus,
        ISoundPlayer sound,
        IAccentColorSource accent,
        TimeProvider time,
        TimeZoneInfo zone)
    {
        _usage = usage;
        _sessions = sessions;
        _settingsStore = settings;
        _provider = provider;
        _hover = hover;
        _ui = ui;
        _focus = focus;
        _sound = sound;
        _accent = accent;
        _time = time;
        _zone = zone;

        RefreshCommand = new RelayCommand(requestRefresh);
        ToggleLockCommand = new RelayCommand(_hover.ToggleLock);
        PeekCommand = new RelayCommand(_hover.Peek);
        DismissSessionCommand = new RelayCommand<string>(id => { if (id is not null) _sessions.Dismiss(id); });
        FocusSessionCommand = new RelayCommand<string>(id => { if (id is not null) _focus.Focus(_sessions.ParentPidOf(id)); });

        _onUsage = _ => Post(Recompute);
        _onSessions = () => Post(OnSessionsChanged);
        _onSettings = _ => Post(Recompute);
        _onHover = () => Post(SyncHover);

        _usage.Changed += _onUsage;
        _sessions.Changed += _onSessions;
        _settingsStore.Changed += _onSettings;
        _hover.Changed += _onHover;

        _watcher.Observe(_sessions.Snapshot());
        Recompute();
        SyncHover();

        _clock = _time.CreateTimer(_ => Post(Recompute), null, ClockInterval, ClockInterval);
    }

    public CellModel Cell { get => _cell; private set => SetProperty(ref _cell, value); }
    public CardModel Card { get => _card; private set => SetProperty(ref _card, value); }
    public Theme Theme { get => _theme; private set => SetProperty(ref _theme, value); }
    public CoreSettings Settings { get => _settings; private set => SetProperty(ref _settings, value); }
    public bool CardVisible { get => _cardVisible; private set => SetProperty(ref _cardVisible, value); }
    public bool Unfolded { get => _unfolded; private set => SetProperty(ref _unfolded, value); }
    public bool Locked { get => _locked; private set => SetProperty(ref _locked, value); }
    public string TrayText { get => _trayText; private set => SetProperty(ref _trayText, value); }

    /// <summary>Une application est en plein écran sur l'écran de la pilule et le réglage le permet : pilule et carte masquées, annonces suspendues.</summary>
    public bool FullscreenActive { get => _fullscreenActive; private set => SetProperty(ref _fullscreenActive, value); }

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ToggleLockCommand { get; }
    public IRelayCommand PeekCommand { get; }
    public IRelayCommand<string> DismissSessionCommand { get; }
    public IRelayCommand<string> FocusSessionCommand { get; }

    /// <summary>Appelé par l'App (thread UI) quand la fenêtre au premier plan entre en plein écran sur l'écran de la pilule ou en sort.</summary>
    public void SetForegroundFullscreen(bool fullscreen)
    {
        if (_disposed || fullscreen == _foregroundFullscreen) return;
        _foregroundFullscreen = fullscreen;
        UpdateFullscreen();
    }

    public void PointerEnteredPill() => _hover.PointerEnteredPill();
    public void PointerLeftPill() => _hover.PointerLeftPill();
    public void PointerEnteredCard() => _hover.PointerEnteredCard();
    public void PointerLeftCard() => _hover.PointerLeftCard();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clock.Dispose();
        _usage.Changed -= _onUsage;
        _sessions.Changed -= _onSessions;
        _settingsStore.Changed -= _onSettings;
        _hover.Changed -= _onHover;
    }

    private void Post(Action action) => _ui.Post(() => { if (!_disposed) action(); });

    private void OnSessionsChanged()
    {
        var transitions = _watcher.Observe(_sessions.Snapshot());
        // En plein écran, les transitions sont consommées sans annonce : elles ne seront pas rejouées ensuite.
        if (transitions.Count > 0 && !FullscreenActive)
        {
            var s = _settingsStore.Current;
            if (s.AutoOpenCard) _hover.Peek();
            if (s.SoundEnabled)
            {
                _sound.Play(transitions.Any(t => t.Kind == TransitionKind.Attention) ? s.AttentionSound : s.DoneSound);
            }
        }
        Recompute();
    }

    private void Recompute()
    {
        var now = _time.GetUtcNow();
        var settings = _settingsStore.Current;
        var theme = Theme.ForPreset(settings.ThemePreset, settings.CustomTheme, _accent.AccentHex);
        var snapshot = _usage.Current;

        Settings = settings;
        UpdateFullscreen();
        Theme = theme;
        Cell = PillPresenter.Cell(snapshot, _provider.HeadlineWindowId, _sessions.Aggregate, theme, settings.CellContent, now);
        Card = CardPresenter.Build(snapshot, _provider.DisplayName, _sessions.Snapshot(), theme, now, _zone);
        TrayText = $"UsageNotch — {_provider.DisplayName} {Cell.PercentText}";
    }

    private void UpdateFullscreen()
    {
        var s = _settingsStore.Current;
        FullscreenActive = _foregroundFullscreen && s.HideInFullscreen && s.Visibility != VisibilityMode.Hidden;
        SyncHover();
    }

    private void SyncHover()
    {
        CardVisible = _hover.CardVisible && !FullscreenActive;
        Unfolded = _hover.Unfolded;
        Locked = _hover.Locked;
    }
}
