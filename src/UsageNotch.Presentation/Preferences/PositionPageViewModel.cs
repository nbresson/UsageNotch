using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Position » : écran d'ancrage, bord, position le long du bord, mode de visibilité, bande repliée.</summary>
public sealed class PositionPageViewModel : ObservableObject, IDisposable
{
    public const double MapWidth = 360;
    public const double MapHeight = 150;
    public const double MapPadding = 6;
    public const string DragHint = "Astuce : Alt + glisser la pilule la déplace le long du bord.";
    public const string FoldedHint = "En mode Replié, seule une fine bande reste au bord ; la pilule se déplie au survol.";
    public const string HiddenHint = "En mode Masqué, rien n'est affiché au bord de l'écran ; l'icône de notification reste visible.";

    private readonly SettingsDraft _draft;
    private readonly IMonitorSource _source;
    private IReadOnlyList<MonitorInfo> _monitors;
    private IReadOnlyList<Choice<string>> _choices;

    public PositionPageViewModel(SettingsDraft draft, IMonitorSource monitors)
    {
        _draft = draft;
        _source = monitors;
        _monitors = monitors.GetMonitors();
        _choices = MonitorChoices.Build(_monitors, draft.Value.MonitorDeviceId);

        RecenterCommand = new RelayCommand(() => Position = 0.5);
        SelectMonitorCommand = new RelayCommand<string>(key => { if (key is not null) MonitorKey = key; });
        RefreshMonitorsCommand = new RelayCommand(RefreshMonitors);

        _draft.Changed += OnDraftChanged;
    }

    public IReadOnlyList<Choice<string>> Monitors => _choices;

    public IReadOnlyList<Choice<ScreenEdge>> Edges => Choices.Edges;

    public IReadOnlyList<Choice<VisibilityMode>> Visibilities => Choices.Visibilities;

    public string MonitorKey
    {
        get => MonitorChoices.KeyFor(_choices, _draft.Value.MonitorDeviceId);
        set
        {
            // Une liste déroulante WPF écrit null quand sa sélection disparaît : ce n'est pas un choix de l'utilisateur.
            if (value is null || value == MonitorKey) return;
            var deviceId = MonitorChoices.DeviceIdFor(value);
            _draft.Edit(s => s with { MonitorDeviceId = deviceId });
        }
    }

    public MonitorMapModel Map => MonitorMap.Layout(_monitors, _draft.Value, MapWidth, MapHeight, MapPadding);

    public ScreenEdge Edge
    {
        get => _draft.Value.Edge;
        set
        {
            if (value == Edge) return;
            _draft.Edit(s => s with { Edge = value });
        }
    }

    public double Position
    {
        get => _draft.Value.PositionFor(_draft.Value.Edge);
        set
        {
            if (Math.Abs(value - Position) < 1e-9) return;
            _draft.Edit(s => s.WithPosition(s.Edge, value));
        }
    }

    public string PositionText => FrenchText.Percent(Position);

    public VisibilityMode Visibility
    {
        get => _draft.Value.Visibility;
        set
        {
            if (value == Visibility) return;
            _draft.Edit(s => s with { Visibility = value });
        }
    }

    public string VisibilityHint => Visibility switch
    {
        VisibilityMode.Folded => FoldedHint,
        VisibilityMode.Hidden => HiddenHint,
        _ => "",
    };

    public double FoldedThickness
    {
        get => _draft.Value.FoldedThicknessPx;
        set
        {
            var px = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (px == _draft.Value.FoldedThicknessPx) return;
            _draft.Edit(s => s with { FoldedThicknessPx = px });
        }
    }

    public string FoldedThicknessText => $"{_draft.Value.FoldedThicknessPx} px";

    public bool FoldedThicknessEnabled => Visibility == VisibilityMode.Folded;

    public IRelayCommand RecenterCommand { get; }

    public IRelayCommand<string> SelectMonitorCommand { get; }

    public IRelayCommand RefreshMonitorsCommand { get; }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private void RefreshMonitors()
    {
        _monitors = _source.GetMonitors();
        RebuildChoices();
        OnPropertyChanged(nameof(Map));
    }

    /// <summary>Remplace la liste seulement si son contenu change : WPF réécrirait la sélection à chaque nouvelle instance.</summary>
    private void RebuildChoices()
    {
        var next = MonitorChoices.Build(_monitors, _draft.Value.MonitorDeviceId);
        if (next.SequenceEqual(_choices)) return;
        _choices = next;
        OnPropertyChanged(nameof(Monitors));
    }

    private void OnDraftChanged()
    {
        RebuildChoices();
        OnPropertyChanged(string.Empty);
    }
}
