using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsageNotch.App.Interop;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Logging;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

public static class AppHost
{
    public static IHost Build(AppArguments args, AppPaths paths, SettingsStore settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ApplicationName = "UsageNotch",
        });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(new FileLoggerProvider(paths.LogsDirectory, TimeProvider.System,
            () => settings.Current.DebugLogging ? LogLevel.Debug : LogLevel.Information));

        var s = builder.Services;
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton(args);
        s.AddSingleton(paths);
        s.AddSingleton(settings);

        s.AddSingleton<SessionStore>();
        s.AddSingleton<ISessionActivity>(sp => sp.GetRequiredService<SessionStore>());
        s.AddSingleton(sp => new UsageStore(paths.UsageFile, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<UsageStore>>()));
        s.AddSingleton(sp => new ClaudeCredentialReader(ClaudeCredentialReader.DefaultDirectory, sp.GetRequiredService<TimeProvider>()));
        s.AddSingleton(_ => new HttpClient { Timeout = ClaudeUsageProvider.Timeout });
        if (args.Demo)
        {
            s.AddSingleton<IUsageProvider, DemoUsageProvider>();
        }
        else
        {
            s.AddSingleton<IUsageProvider, ClaudeUsageProvider>();
        }

        s.AddSingleton<UsagePoller>();
        s.AddHostedService(sp => sp.GetRequiredService<UsagePoller>());
        s.AddHostedService<SessionSweeper>();
        s.AddSingleton(sp => new HookListener(settings.Current.Port, sp.GetRequiredService<SessionStore>(), sp.GetRequiredService<ILogger<HookListener>>()));
        s.AddHostedService(sp => sp.GetRequiredService<HookListener>());
        s.AddSingleton(sp => new HookInstaller(paths.ClaudeSettingsFile, paths.HookExe, sp.GetRequiredService<TimeProvider>()));

        s.AddSingleton<MonitorService>();
        s.AddSingleton<HoverController>();
        s.AddSingleton<IUiDispatcher>(_ => new WpfDispatcher(Application.Current.Dispatcher));
        s.AddSingleton<ISessionFocus, TerminalFocus>();
        s.AddSingleton<ISoundPlayer, SystemSoundPlayer>();
        s.AddSingleton<IAccentColorSource, SystemAccentColor>();

        s.AddSingleton(sp => new NotchViewModel(
            sp.GetRequiredService<UsageStore>(),
            sp.GetRequiredService<SessionStore>(),
            settings,
            sp.GetRequiredService<IUsageProvider>(),
            () => sp.GetRequiredService<UsagePoller>().RequestRefresh(),
            sp.GetRequiredService<HoverController>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<ISessionFocus>(),
            sp.GetRequiredService<ISoundPlayer>(),
            sp.GetRequiredService<IAccentColorSource>(),
            sp.GetRequiredService<TimeProvider>(),
            TimeZoneInfo.Local));

        s.AddSingleton<NotchShell>();
        return builder.Build();
    }
}
