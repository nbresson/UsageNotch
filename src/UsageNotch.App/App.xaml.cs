using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.App.Hosting;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation;

namespace UsageNotch.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private IHost? _host;
    private NotchShell? _shell;
    private IDisposable? _demo;
    private ILogger<App>? _log;
    private bool _quitting;
    private bool _repeatedFailureHandled;
    private readonly Queue<DateTimeOffset> _recentUiFailures = new();

    private static readonly TimeSpan RepeatedFailureWindow = TimeSpan.FromSeconds(10);
    private const int RepeatedFailureThreshold = 3;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = AppArguments.Parse(e.Args);
        var paths = AppPaths.For(args.Demo);
        var settings = new SettingsStore(paths.SettingsFile, NullLogger<SettingsStore>.Instance);
        settings.Load();

        // Une démo ne doit jamais partager le port du vrai récepteur de hooks : elle recevrait les événements du
        // vrai Claude Code, ou bloquerait la vraie application si les deux tournent en même temps.
        if (args.Demo && settings.Current.Port == Settings.DefaultPort)
        {
            settings.Save(settings.Current with { Port = AppPaths.DemoPort });
        }

        if (args.Doctor)
        {
            DoctorCommand.Run(paths, settings);
            Shutdown(0);
            return;
        }

        // Une démo ne doit pas non plus partager le mutex de la vraie application : les deux doivent pouvoir
        // tourner en même temps sans se signaler l'une l'autre.
        _instance = new SingleInstance(args.Demo ? @"Local\UsageNotch-demo" : @"Local\UsageNotch");
        if (!_instance.IsFirst)
        {
            if (!args.FromHook) await SingleInstance.SignalExistingAsync(settings.Current.Port);
            Shutdown(0);
            return;
        }

        // Contrat du Plan 1 : un démarrage manuel réautorise le hook à relancer l'application.
        if (!args.FromHook && !settings.Current.AutoLaunch)
        {
            settings.Save(settings.Current with { AutoLaunch = true });
        }

        try
        {
            _host = AppHost.Build(args, paths, settings);
            _log = _host.Services.GetRequiredService<ILogger<App>>();
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
                _log?.LogCritical(ev.ExceptionObject as Exception, "Exception non gérée");
            TaskScheduler.UnobservedTaskException += (_, ev) =>
            {
                _log?.LogError(ev.Exception, "Tâche non observée");
                ev.SetObserved();
            };

            // Contrat du Plan 1 : tout ce qui s'abonne aux magasins est créé avant StartAsync.
            _shell = _host.Services.GetRequiredService<NotchShell>();
            _shell.Start();
            if (args.Demo)
            {
                _demo = DemoMode.Start(_host.Services.GetRequiredService<SessionStore>(), TimeProvider.System);
            }

            await _host.StartAsync();
            _log.LogInformation("UsageNotch démarré (démo : {Demo}, lancé par le hook : {FromHook})", args.Demo, args.FromHook);
        }
        catch (Exception ex)
        {
            // Un échec ici laisserait le mutex tenu par un processus invisible pour toujours (ShutdownMode
            // OnExplicitShutdown) : on journalise du mieux possible, on libère ce qui a pu être créé, et on quitte.
            if (_log is not null)
            {
                _log.LogCritical(ex, "Échec du démarrage");
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(paths.LogsDirectory);
                    File.AppendAllText(Path.Combine(paths.LogsDirectory, "startup-error.txt"),
                        $"{DateTimeOffset.UtcNow:O} {ex}{Environment.NewLine}");
                }
                catch (Exception ioEx) when (ioEx is IOException or UnauthorizedAccessException)
                {
                }
            }

            try { _demo?.Dispose(); }
            catch (Exception disposeEx) { _log?.LogWarning(disposeEx, "Échec de l'arrêt de la démo après une erreur de démarrage"); }
            try { _shell?.Dispose(); }
            catch (Exception disposeEx) { _log?.LogWarning(disposeEx, "Échec de l'arrêt de la coquille après une erreur de démarrage"); }
            try { _host?.Dispose(); }
            catch (Exception disposeEx) { _log?.LogWarning(disposeEx, "Échec de la fermeture de l'hôte après une erreur de démarrage"); }

            Shutdown(1);
        }
    }

    /// <summary>« Quitter » : le hook ne relancera plus l'application jusqu'au prochain démarrage manuel.</summary>
    public async Task QuitAsync(bool userInitiated)
    {
        if (_quitting) return;
        _quitting = true;

        try
        {
            if (userInitiated && _host is not null)
            {
                var settings = _host.Services.GetRequiredService<SettingsStore>();
                settings.Save(settings.Current with { AutoLaunch = false });
            }

            _demo?.Dispose();
            _shell?.Dispose();
            if (_host is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await _host.StopAsync(timeout.Token); }
                catch (OperationCanceledException) { }
                _host.Dispose();
            }
            _log?.LogInformation("UsageNotch arrêté");
        }
        catch (Exception e)
        {
            _log?.LogError(e, "Erreur pendant l'arrêt");
        }
        finally
        {
            // Toujours quitter, même si l'arrêt propre a échoué : rester ouvert reviendrait à ignorer « Quitter ».
            Shutdown(0);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.LogError(e.Exception, "Exception non gérée sur le thread UI");
        e.Handled = true;

        // Spec §9 : une erreur isolée est journalisée et ignorée ; des erreurs répétées ferment l'application.
        var now = TimeProvider.System.GetUtcNow();
        _recentUiFailures.Enqueue(now);
        while (_recentUiFailures.Count > 0 && now - _recentUiFailures.Peek() > RepeatedFailureWindow)
        {
            _recentUiFailures.Dequeue();
        }
        if (_recentUiFailures.Count < RepeatedFailureThreshold || _repeatedFailureHandled || _quitting) return;

        _repeatedFailureHandled = true;
        _log?.LogCritical("{Count} exceptions sur le thread UI en moins de {Seconds} s : arrêt de l'application",
            _recentUiFailures.Count, RepeatedFailureWindow.TotalSeconds);
        MessageBox.Show("UsageNotch a rencontré des erreurs répétées et va se fermer. Les détails sont dans le journal.",
            "UsageNotch", MessageBoxButton.OK, MessageBoxImage.Error);
        _ = QuitAsync(userInitiated: false);
    }
}
