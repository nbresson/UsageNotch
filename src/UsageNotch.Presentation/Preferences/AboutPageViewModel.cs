using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « À propos » : version, dossiers, niveau de journalisation, diagnostic.</summary>
public sealed class AboutPageViewModel : ObservableObject, IDisposable
{
    public const string DebugNote =
        "Le niveau Debug ajoute le détail des requêtes et des événements de hooks, jamais de jeton ni de contenu de prompt.";

    private readonly SettingsDraft _draft;
    private readonly SettingsEnvironment _environment;
    private string _doctorError = "";

    public AboutPageViewModel(SettingsDraft draft, IShellActions shell, SettingsEnvironment environment)
    {
        _draft = draft;
        _environment = environment;

        OpenDataFolderCommand = new RelayCommand(() => shell.OpenFolder(environment.DataDirectory));
        OpenLogsFolderCommand = new RelayCommand(() => shell.OpenFolder(environment.LogsDirectory));
        OpenSettingsFileCommand = new RelayCommand(() => shell.OpenFile(environment.SettingsFile));
        RunDoctorCommand = new RelayCommand(() => DoctorError = shell.RunDoctor() ?? "");

        _draft.Changed += OnDraftChanged;
    }

    public string VersionText => $"UsageNotch {_environment.Version}";

    public string DataDirectory => _environment.DataDirectory;

    public string LogsDirectory => _environment.LogsDirectory;

    public string SettingsFile => _environment.SettingsFile;

    public string DemoHint => _environment.Demo
        ? $"Mode démo : réglages, lecture et journaux de test dans {_environment.DataDirectory}."
        : "";

    public bool DebugLogging
    {
        get => _draft.Value.DebugLogging;
        set
        {
            if (value == DebugLogging) return;
            _draft.Edit(s => s with { DebugLogging = value });
        }
    }

    public string DoctorError
    {
        get => _doctorError;
        private set => SetProperty(ref _doctorError, value);
    }

    public IRelayCommand OpenDataFolderCommand { get; }

    public IRelayCommand OpenLogsFolderCommand { get; }

    public IRelayCommand OpenSettingsFileCommand { get; }

    public IRelayCommand RunDoctorCommand { get; }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private void OnDraftChanged() => OnPropertyChanged(nameof(DebugLogging));
}
