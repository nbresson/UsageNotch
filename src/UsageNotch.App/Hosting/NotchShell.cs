using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Views;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    NotchPlacer placer,
    SettingsStore settings,
    AppPaths paths,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;
    private PillWindow? _pill;

    public void Start()
    {
        _onOpenSettings = () => ui.Post(() => viewModel.PeekCommand.Execute(null));
        listener.OpenSettingsRequested += _onOpenSettings;

        _pill = new PillWindow(viewModel, placer, settings, () => ((App)Application.Current).QuitAsync(userInitiated: true), OpenSettingsFile);
        if (settings.Current.Visibility != VisibilityMode.Hidden) _pill.Show();

        logger.LogInformation("Coquille démarrée");
    }

    /// <summary>Plan 2 : les réglages s'éditent dans settings.json ; le Plan 3 remplacera ceci par la fenêtre de réglages.</summary>
    private void OpenSettingsFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{paths.SettingsFile}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            logger.LogWarning(e, "Impossible d'ouvrir {Path}", paths.SettingsFile);
        }
    }

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        _pill?.Close();
        viewModel.Dispose();
    }
}
