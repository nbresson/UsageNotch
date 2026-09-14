using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.App.Hosting;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation;

namespace UsageNotch.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = AppArguments.Parse(e.Args);
        var paths = AppPaths.For(args.Demo);
        var settings = new SettingsStore(paths.SettingsFile, NullLogger<SettingsStore>.Instance);
        settings.Load();
        if (args.Doctor) DoctorCommand.Run(paths, settings);
        Shutdown(0);
    }
}
