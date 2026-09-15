using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Page « Claude Code » : état et installation des hooks, port du récepteur, chemins utiles.</summary>
public sealed class ClaudeCodePageViewModel : ObservableObject, IDisposable
{
    public const string PortRangeError = "Le port doit être un nombre entre 1024 et 65535.";
    public const string RestartNote = "Le nouveau port sera utilisé au prochain démarrage d'UsageNotch.";
    public const string DemoNote = "Mode démo : les hooks sont écrits dans un fichier de test, jamais dans la configuration de Claude Code.";

    private readonly SettingsDraft _draft;
    private readonly IHookSetup _hooks;
    private readonly SettingsEnvironment _environment;
    private bool _installed;
    private long _statusVersion;
    private string _lastMessage = "";
    private bool _lastActionFailed;
    private string _portText;
    private string _portError = "";

    public ClaudeCodePageViewModel(SettingsDraft draft, IHookSetup hooks, IShellActions shell, SettingsEnvironment environment)
    {
        _draft = draft;
        _hooks = hooks;
        _environment = environment;
        _installed = hooks.IsInstalled();
        _portText = FormatPort(draft.Value.Port);

        InstallCommand = new RelayCommand(() => Apply(_hooks.Install()), () => !_installed && _hooks.HookExeExists);
        UninstallCommand = new RelayCommand(() => Apply(_hooks.Uninstall()), () => _installed);
        RefreshCommand = new RelayCommand(Refresh);
        OpenClaudeFolderCommand = new RelayCommand(() => shell.OpenFolder(Path.GetDirectoryName(_hooks.SettingsPath) ?? _hooks.SettingsPath));

        _draft.Changed += OnDraftChanged;
    }

    public bool HooksInstalled => _installed;

    public string HooksStatus => _installed ? "Installés" : "Non installés";

    public string LastMessage
    {
        get => _lastMessage;
        private set => SetProperty(ref _lastMessage, value);
    }

    public bool LastActionFailed
    {
        get => _lastActionFailed;
        private set => SetProperty(ref _lastActionFailed, value);
    }

    public string ClaudeSettingsPath => _hooks.SettingsPath;

    public string HookExePath => _hooks.HookExePath;

    public string HookExeStatus => _hooks.HookExeExists ? "Présent" : "Introuvable : les hooks ne peuvent pas être installés.";

    public string DemoHint => _environment.Demo ? DemoNote : "";

    /// <summary>Texte saisi ; un port valide modifie le brouillon, sinon <see cref="PortError"/> explique pourquoi.</summary>
    public string PortText
    {
        get => _portText;
        set
        {
            _portText = value ?? "";
            if (int.TryParse(_portText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1024 and <= 65535)
            {
                PortError = "";
                if (port != _draft.Value.Port) _draft.Edit(s => s with { Port = port });
            }
            else
            {
                PortError = PortRangeError;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(PortNote));
        }
    }

    public string PortError
    {
        get => _portError;
        private set => SetProperty(ref _portError, value);
    }

    public string PortNote =>
        _draft.Value.Port != _environment.ListeningPort ? RestartNote
        : !_environment.Listening ? $"Le port {_environment.ListeningPort} est indisponible : une autre application l'utilise peut-être. Choisissez-en un autre, puis redémarrez UsageNotch."
        : "";

    public IRelayCommand InstallCommand { get; }

    public IRelayCommand UninstallCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand OpenClaudeFolderCommand { get; }

    public void Dispose() => _draft.Changed -= OnDraftChanged;

    private static string FormatPort(int port) => port.ToString(CultureInfo.InvariantCulture);

    private void Apply(HookSetupResult result)
    {
        LastMessage = result.Message;
        LastActionFailed = !result.Succeeded;
        Refresh();
    }

    /// <summary>
    /// Relit l'état des hooks hors du thread UI : la lecture de settings.json de Claude Code ne bloque pas la fenêtre
    /// quand elle revient au premier plan. Une action ou une relecture lancée entre-temps l'emporte : un résultat devenu
    /// périmé est ignoré. À appeler depuis le thread UI.
    /// </summary>
    public async Task RefreshInBackgroundAsync()
    {
        var version = ++_statusVersion;
        var installed = await Task.Run(() => _hooks.IsInstalled());
        if (version != _statusVersion) return;
        SetInstalled(installed);
    }

    private void Refresh()
    {
        _statusVersion++;
        SetInstalled(_hooks.IsInstalled());
    }

    private void SetInstalled(bool installed)
    {
        _installed = installed;
        OnPropertyChanged(nameof(HooksInstalled));
        OnPropertyChanged(nameof(HooksStatus));
        OnPropertyChanged(nameof(HookExeStatus));
        InstallCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Une saisie invalide en cours n'est pas remplacée ; sinon le texte suit le port enregistré ailleurs.</summary>
    private void OnDraftChanged()
    {
        if (PortError.Length == 0) _portText = FormatPort(_draft.Value.Port);
        OnPropertyChanged(string.Empty);
    }
}
