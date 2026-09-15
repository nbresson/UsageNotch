using CommunityToolkit.Mvvm.ComponentModel;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

public enum SettingsPageKind
{
    Appearance,
    Position,
    Behavior,
    ClaudeCode,
    About,
}

/// <summary>Une entrée de la navigation latérale. <see cref="ToString"/> rend le titre, lu par l'accessibilité.</summary>
public sealed record SettingsPage(SettingsPageKind Kind, string Title, object ViewModel)
{
    public override string ToString() => Title;
}

/// <summary>La fenêtre de réglages : navigation entre les cinq pages (spec §7), aperçu commun, brouillon partagé.</summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsDraft _draft;
    private readonly IAccentColorSource _accent;
    private SettingsPage _selectedPage;
    private PreviewModel _preview;
    private bool _disposed;

    public SettingsViewModel(
        SettingsDraft draft,
        IAccentColorSource accent,
        AppearancePageViewModel appearance,
        PositionPageViewModel position,
        BehaviorPageViewModel behavior,
        ClaudeCodePageViewModel claudeCode,
        AboutPageViewModel about)
    {
        _draft = draft;
        _accent = accent;
        Appearance = appearance;
        Position = position;
        Behavior = behavior;
        ClaudeCode = claudeCode;
        About = about;

        Pages =
        [
            new SettingsPage(SettingsPageKind.Appearance, "Apparence", appearance),
            new SettingsPage(SettingsPageKind.Position, "Position", position),
            new SettingsPage(SettingsPageKind.Behavior, "Comportement", behavior),
            new SettingsPage(SettingsPageKind.ClaudeCode, "Claude Code", claudeCode),
            new SettingsPage(SettingsPageKind.About, "À propos", about),
        ];
        _selectedPage = Pages[0];
        _preview = SettingsPreview.Build(draft.Value, accent.AccentHex);

        _draft.Changed += OnDraftChanged;
    }

    public static SettingsViewModel Create(
        SettingsStore store,
        IUiDispatcher ui,
        TimeProvider time,
        IAccentColorSource accent,
        IColorPicker picker,
        IMonitorSource monitors,
        ISoundPlayer sound,
        IAutoStart autoStart,
        IHookSetup hooks,
        IShellActions shell,
        SettingsEnvironment environment)
    {
        var draft = new SettingsDraft(store, ui, time);
        return new SettingsViewModel(
            draft,
            accent,
            new AppearancePageViewModel(draft, accent, picker),
            new PositionPageViewModel(draft, monitors),
            new BehaviorPageViewModel(draft, sound, autoStart),
            new ClaudeCodePageViewModel(draft, hooks, shell, environment),
            new AboutPageViewModel(draft, shell, environment));
    }

    public AppearancePageViewModel Appearance { get; }

    public PositionPageViewModel Position { get; }

    public BehaviorPageViewModel Behavior { get; }

    public ClaudeCodePageViewModel ClaudeCode { get; }

    public AboutPageViewModel About { get; }

    public IReadOnlyList<SettingsPage> Pages { get; }

    public SettingsPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null) return;
            SetProperty(ref _selectedPage, value);
        }
    }

    public PreviewModel Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public void Select(SettingsPageKind kind) => SelectedPage = Pages.First(p => p.Kind == kind);

    /// <summary>Enregistre tout de suite les modifications en attente (perte de focus de la fenêtre).</summary>
    public void Flush() => _draft.Flush();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _draft.Changed -= OnDraftChanged;
        Appearance.Dispose();
        Position.Dispose();
        Behavior.Dispose();
        ClaudeCode.Dispose();
        About.Dispose();
        _draft.Dispose();
    }

    private void OnDraftChanged() => Preview = SettingsPreview.Build(_draft.Value, _accent.AccentHex);
}
