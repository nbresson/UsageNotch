using CommunityToolkit.Mvvm.ComponentModel;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Apparence » : préréglage, couleurs, opacité, seuils, échelle, contenu de la pilule et coloration des anneaux.</summary>
public sealed class AppearancePageViewModel : ObservableObject, IDisposable
{
    public const string CustomHint =
        "Modifier une couleur, l'opacité ou un seuil passe au thème Personnalisé, initialisé depuis le thème affiché.";

    private readonly SettingsDraft _draft;
    private readonly IAccentColorSource _accent;

    public AppearancePageViewModel(SettingsDraft draft, IAccentColorSource accent, IColorPicker picker)
    {
        _draft = draft;
        _accent = accent;

        ColorSlot Slot(string key, string label, Func<Theme, string> get, Func<Theme, string, Theme> set) =>
            new(key, label, get, set, () => Theme, EditTheme, picker);

        Colors =
        [
            Slot("PillBackground", "Fond de la pilule", t => t.PillBackground, (t, h) => t with { PillBackground = h }),
            Slot("PillBorder", "Contour de la pilule", t => t.PillBorder, (t, h) => t with { PillBorder = h }),
            Slot("RingTrack", "Piste de l'anneau", t => t.RingTrack, (t, h) => t with { RingTrack = h }),
            Slot("LevelAmple", "Niveau modéré", t => t.LevelAmple, (t, h) => t with { LevelAmple = h }),
            Slot("LevelWatch", "Niveau vigilance", t => t.LevelWatch, (t, h) => t with { LevelWatch = h }),
            Slot("LevelCritical", "Niveau critique", t => t.LevelCritical, (t, h) => t with { LevelCritical = h }),
            Slot("RingSession", "Anneau session", t => t.RingSession, (t, h) => t with { RingSession = h }),
            Slot("RingWeeklyAll", "Anneau hebdomadaire", t => t.RingWeeklyAll, (t, h) => t with { RingWeeklyAll = h }),
            Slot("RingWeeklyScoped", "Anneau hebdo. par modèle", t => t.RingWeeklyScoped, (t, h) => t with { RingWeeklyScoped = h }),
            Slot("Running", "Session en cours", t => t.Running, (t, h) => t with { Running = h }),
            Slot("Attention", "Session en attente", t => t.Attention, (t, h) => t with { Attention = h }),
            Slot("Done", "Session terminée", t => t.Done, (t, h) => t with { Done = h }),
            Slot("Text", "Texte", t => t.Text, (t, h) => t with { Text = h }),
        ];

        _draft.Changed += OnDraftChanged;
    }

    public IReadOnlyList<Choice<ThemePreset>> Presets => Choices.ThemePresets;

    public IReadOnlyList<Choice<CellContent>> CellContents => Choices.CellContents;

    public IReadOnlyList<Choice<RingColoring>> RingColorings => Choices.RingColorings;

    public IReadOnlyList<ColorSlot> Colors { get; }

    /// <summary>Le thème affiché : celui du préréglage choisi, avec l'accent système lu maintenant.</summary>
    public Theme Theme => EffectiveTheme(_draft.Value, _accent.AccentHex);

    public ThemePreset Preset
    {
        get => _draft.Value.ThemePreset;
        set
        {
            if (value == Preset) return;
            var accent = _accent.AccentHex;
            _draft.Edit(s => value == ThemePreset.Custom && s.ThemePreset != ThemePreset.Custom
                ? s with { ThemePreset = ThemePreset.Custom, CustomTheme = EffectiveTheme(s, accent) }
                : s with { ThemePreset = value });
        }
    }

    public double PillOpacity
    {
        get => Theme.PillOpacity;
        set
        {
            if (Same(value, PillOpacity)) return;
            EditTheme(t => t with { PillOpacity = value });
        }
    }

    public string PillOpacityText => FrenchText.Percent(PillOpacity);

    public double ThresholdWatch
    {
        get => Theme.ThresholdWatch;
        set
        {
            if (Same(value, ThresholdWatch)) return;
            EditTheme(t => t with { ThresholdWatch = value });
        }
    }

    public string ThresholdWatchText => FrenchText.Percent(ThresholdWatch);

    public double ThresholdCritical
    {
        get => Theme.ThresholdCritical;
        set
        {
            if (Same(value, ThresholdCritical)) return;
            EditTheme(t => t with { ThresholdCritical = value });
        }
    }

    public string ThresholdCriticalText => FrenchText.Percent(ThresholdCritical);

    public double Scale
    {
        get => _draft.Value.Scale;
        set
        {
            if (Same(value, Scale)) return;
            _draft.Edit(s => s with { Scale = value });
        }
    }

    /// <summary>L'échelle dépasse 100 % : pas de <see cref="FrenchText.Percent"/>, qui borne à 1.</summary>
    public string ScaleText => $"{(int)Math.Round(Scale * 100, MidpointRounding.AwayFromZero)}{FrenchText.Nbsp}%";

    public CellContent CellContent
    {
        get => _draft.Value.CellContent;
        set
        {
            if (value == CellContent) return;
            _draft.Edit(s => s with { CellContent = value });
        }
    }

    public RingColoring Coloring
    {
        get => _draft.Value.Coloring;
        set
        {
            if (value == Coloring) return;
            _draft.Edit(s => s with { Coloring = value });
        }
    }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private static Theme EffectiveTheme(CoreSettings s, string? accent) => Theme.ForPreset(s.ThemePreset, s.CustomTheme, accent);

    /// <summary>Toute retouche du thème passe au thème Personnalisé, initialisé depuis le thème affiché.</summary>
    private void EditTheme(Func<Theme, Theme> change)
    {
        var accent = _accent.AccentHex;
        _draft.Edit(s => s with
        {
            ThemePreset = ThemePreset.Custom,
            CustomTheme = change(s.ThemePreset == ThemePreset.Custom ? s.CustomTheme : EffectiveTheme(s, accent)),
        });
    }

    private static bool Same(double a, double b) => Math.Abs(a - b) < 1e-9;

    private void OnDraftChanged()
    {
        OnPropertyChanged(string.Empty);
        foreach (var slot in Colors) slot.Refresh();
    }
}
