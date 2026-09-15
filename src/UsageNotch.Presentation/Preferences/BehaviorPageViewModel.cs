using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Comportement » : auto-ouverture de la carte, sons, démarrer avec Windows, icône de notification.</summary>
public sealed class BehaviorPageViewModel : ObservableObject, IDisposable
{
    public const string AutoStartUnavailableNote = "Indisponible en mode démo.";
    public const string TrayLockedNote = "Toujours visible en mode Masqué : c'est alors le seul accès à UsageNotch.";
    public const string AlertsNeedTrayIconNote = "Les alertes passent par l'icône de notification : masquée, elles ne s'affichent pas.";

    private readonly SettingsDraft _draft;
    private readonly IAutoStart _autoStart;
    private bool _autoStartEnabled;
    private string _autoStartError = "";

    public BehaviorPageViewModel(SettingsDraft draft, ISoundPlayer sound, IAutoStart autoStart)
    {
        _draft = draft;
        _autoStart = autoStart;
        _autoStartEnabled = autoStart.IsAvailable && autoStart.IsEnabled();

        PlayDoneSoundCommand = new RelayCommand(() => sound.Play(DoneSound));
        PlayAttentionSoundCommand = new RelayCommand(() => sound.Play(AttentionSound));

        _draft.Changed += OnDraftChanged;
    }

    public bool AutoOpenCard
    {
        get => _draft.Value.AutoOpenCard;
        set
        {
            if (value == AutoOpenCard) return;
            _draft.Edit(s => s with { AutoOpenCard = value });
        }
    }

    public bool SoundEnabled
    {
        get => _draft.Value.SoundEnabled;
        set
        {
            if (value == SoundEnabled) return;
            _draft.Edit(s => s with { SoundEnabled = value });
        }
    }

    public IReadOnlyList<Choice<string>> Sounds => Choices.Sounds;

    public string DoneSound
    {
        get => _draft.Value.DoneSound;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == DoneSound) return;
            _draft.Edit(s => s with { DoneSound = value });
        }
    }

    public string AttentionSound
    {
        get => _draft.Value.AttentionSound;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == AttentionSound) return;
            _draft.Edit(s => s with { AttentionSound = value });
        }
    }

    public IRelayCommand PlayDoneSoundCommand { get; }

    public IRelayCommand PlayAttentionSoundCommand { get; }

    public bool AutoStartAvailable => _autoStart.IsAvailable;

    public string AutoStartNote => AutoStartAvailable ? "" : AutoStartUnavailableNote;

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (!AutoStartAvailable || value == _autoStartEnabled) return;

            var error = _autoStart.TrySet(value);
            if (error is null)
            {
                _autoStartEnabled = _autoStart.IsEnabled();
                AutoStartError = "";
            }
            else
            {
                AutoStartError = error;
            }
            // Toujours notifier : en cas d'échec, la case cochée par l'utilisateur doit revenir à l'état réel.
            OnPropertyChanged();
        }
    }

    /// <summary>Relit l'état réel (il a pu changer depuis le menu de l'icône de notification) ; aucune lecture en mode démo.</summary>
    public void RefreshAutoStart()
    {
        _autoStartEnabled = _autoStart.IsAvailable && _autoStart.IsEnabled();
        OnPropertyChanged(nameof(AutoStartEnabled));
    }

    public string AutoStartError
    {
        get => _autoStartError;
        private set => SetProperty(ref _autoStartError, value);
    }

    public bool TrayIconVisible
    {
        get => _draft.Value.TrayIconVisible;
        set
        {
            if (value == TrayIconVisible) return;
            if (!TrayIconEditable)
            {
                OnPropertyChanged();
                return;
            }
            _draft.Edit(s => s with { TrayIconVisible = value });
        }
    }

    public bool TrayIconEditable => _draft.Value.Visibility != VisibilityMode.Hidden;

    public string TrayIconNote => TrayIconEditable ? "" : TrayLockedNote;

    public bool ThresholdNotifications
    {
        get => _draft.Value.ThresholdNotifications;
        set
        {
            if (value == ThresholdNotifications) return;
            _draft.Edit(s => s with { ThresholdNotifications = value });
        }
    }

    public double NotifyThreshold
    {
        get => _draft.Value.NotifyThreshold;
        set
        {
            if (Math.Abs(value - NotifyThreshold) < 1e-9) return;
            _draft.Edit(s => s with { NotifyThreshold = value });
        }
    }

    public string NotifyThresholdText => UsageNotch.Presentation.Formatting.FrenchText.Percent(NotifyThreshold);

    public bool NotifyThresholdEditable => ThresholdNotifications;

    /// <summary>Les notifications Windows sont émises par l'icône : elles ne s'affichent pas quand l'icône est masquée.</summary>
    public string ThresholdNotificationsNote =>
        ThresholdNotifications && !_draft.Value.TrayIconVisible && _draft.Value.Visibility != VisibilityMode.Hidden
            ? AlertsNeedTrayIconNote
            : "";

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private void OnDraftChanged() => OnPropertyChanged(string.Empty);
}
