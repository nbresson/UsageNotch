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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = AppArguments.Parse(e.Args);
        var paths = AppPaths.For(args.Demo);
        var settings = new SettingsStore(paths.SettingsFile, NullLogger<SettingsStore>.Instance);
        settings.Load();

        if (args.Doctor)
        {
            DoctorCommand.Run(paths, settings);
            Shutdown(0);
            return;
        }

        _instance = new SingleInstance(@"Local\UsageNotch");
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

    /// <summary>« Quitter » : le hook ne relancera plus l'application jusqu'au prochain démarrage manuel.</summary>
    public async Task QuitAsync(bool userInitiated)
    {
        if (_quitting) return;
        _quitting = true;

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
        Shutdown(0);
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
    }
}
