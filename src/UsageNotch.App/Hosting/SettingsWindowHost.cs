using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Interop;
using UsageNotch.App.Views.Preferences;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation;
using UsageNotch.Presentation.Preferences;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>
/// Une seule fenêtre de réglages à la fois : la redemander la ramène au premier plan. Chaque ouverture crée un ViewModel
/// neuf ; la fermeture le libère, ce qui enregistre les modifications en attente. À utiliser depuis le thread UI.
/// </summary>
public sealed class SettingsWindowHost(
    SettingsStore settings,
    IUiDispatcher ui,
    TimeProvider time,
    IAccentColorSource accent,
    NativeColorPicker picker,
    IMonitorSource monitors,
    ISoundPlayer sound,
    IAutoStart autoStart,
    IHookSetup hooks,
    IShellActions shell,
    AppPaths paths,
    AppArguments args,
    HookListener listener,
    ILogger<SettingsWindowHost> logger) : IDisposable
{
    private SettingsWindow? _window;
    private SettingsViewModel? _vm;

    public void Show(SettingsPageKind? page = null)
    {
        if (_window is null || _vm is null)
        {
            _vm = SettingsViewModel.Create(settings, ui, time, accent, picker, monitors, sound, autoStart, hooks, shell, BuildEnvironment());
            _window = new SettingsWindow(_vm, picker);
            _window.Closed += OnClosed;
            _window.Show();
            logger.LogInformation("Fenêtre de réglages ouverte");
        }

        if (page is { } kind) _vm.Select(kind);
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Dispose() => _window?.Close();

    private SettingsEnvironment BuildEnvironment() => new(
        Version: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?",
        Demo: args.Demo,
        DataDirectory: paths.DataDirectory,
        LogsDirectory: paths.LogsDirectory,
        SettingsFile: paths.SettingsFile,
        ListeningPort: listener.Port,
        Listening: listener.IsListening);

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_window is not null) _window.Closed -= OnClosed;
        _window = null;
        var vm = _vm;
        _vm = null;
        vm?.Dispose();
        logger.LogInformation("Fenêtre de réglages fermée");
    }
}
