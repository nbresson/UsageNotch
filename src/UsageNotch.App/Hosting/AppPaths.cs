using System.IO;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Hosting;

public sealed record AppPaths(
    string DataDirectory,
    string SettingsFile,
    string UsageFile,
    string LogsDirectory,
    string HookExe,
    string ClaudeSettingsFile)
{
    /// <summary>Port utilisé par le récepteur de hooks en démo, distinct du port réel : une démo ne doit jamais
    /// recevoir les événements du vrai Claude Code, ni bloquer le port de la vraie application.</summary>
    public const int DemoPort = 48667;

    /// <summary>En démo, tout vit dans %TEMP%\UsageNotch-demo : ni les réglages, ni la lecture, ni la config Claude Code réelles ne sont touchés.</summary>
    public static AppPaths For(bool demo)
    {
        var data = demo
            ? Path.Combine(Path.GetTempPath(), "UsageNotch-demo")
            : SettingsStore.DefaultDirectory;
        return new AppPaths(
            DataDirectory: data,
            SettingsFile: Path.Combine(data, "settings.json"),
            UsageFile: Path.Combine(data, "usage.json"),
            LogsDirectory: Path.Combine(data, "logs"),
            HookExe: Path.Combine(AppContext.BaseDirectory, "UsageNotch.Hook.exe"),
            ClaudeSettingsFile: demo ? Path.Combine(data, "claude-settings.json") : HookInstaller.DefaultSettingsPath);
    }
}
