using System.Windows;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Tray;
using UsageNotch.App.Views;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    NotchPlacer placer,
    SettingsStore settings,
    TrayIconService tray,
    SettingsFileWatcher watcher,
    SettingsWindowHost settingsWindow,
    ThresholdNotifications thresholds,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;
    private PillWindow? _pill;
    private CardWindow? _card;

    public void Start()
    {
        // Seconde instance lancée à la main (spec §8) : l'événement arrive sur un thread du récepteur.
        _onOpenSettings = () => ui.Post(OpenSettings);
        listener.OpenSettingsRequested += _onOpenSettings;

        _pill = new PillWindow(viewModel, placer, settings, QuitAsync, OpenSettings);
        _card = new CardWindow(viewModel, placer, _pill);
        if (settings.Current.Visibility != VisibilityMode.Hidden) _pill.Show();

        tray.Start(QuitAsync, OpenSettings);
        watcher.Start();
        // Après l'icône : les alertes de seuil sont des notifications de l'icône.
        thresholds.Start();

        logger.LogInformation("Coquille démarrée");
    }

    private static Task QuitAsync() => ((App)Application.Current).QuitAsync(userInitiated: true);

    private void OpenSettings() => settingsWindow.Show();

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        // D'abord la fenêtre de réglages : sa fermeture enregistre ses modifications par-dessus l'état courant
        // (dont AutoLaunch = false posé par Quitter).
        settingsWindow.Dispose();
        thresholds.Dispose();
        watcher.Dispose();
        tray.Dispose();
        _card?.Close();
        _pill?.Close();
        viewModel.Dispose();
    }
}
