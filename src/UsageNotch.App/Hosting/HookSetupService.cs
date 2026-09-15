using System.IO;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Hooks;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>Installation des hooks : les exceptions de <see cref="HookInstaller"/> deviennent des messages en français.</summary>
public sealed class HookSetupService(HookInstaller installer, ILogger<HookSetupService> logger) : IHookSetup
{
    private const string FailurePrefix = "Impossible de modifier les hooks Claude Code : ";

    public string SettingsPath => installer.SettingsPath;

    public string HookExePath => installer.HookExePath;

    public bool HookExeExists => File.Exists(installer.HookExePath);

    public bool IsInstalled()
    {
        try
        {
            return installer.IsInstalled();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public HookSetupResult Install() => Run("installation", installer.Install);

    public HookSetupResult Uninstall() => Run("désinstallation", installer.Uninstall);

    private HookSetupResult Run(string action, Func<string> operation)
    {
        try
        {
            var message = operation();
            logger.LogInformation("Hooks Claude Code, {Action} : {Message}", action, message);
            return new HookSetupResult(true, message);
        }
        catch (FileNotFoundException)
        {
            return Fail(action, $"exécutable hook introuvable : {installer.HookExePath}");
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Fail(action, e.Message);
        }
    }

    private HookSetupResult Fail(string action, string reason)
    {
        logger.LogWarning("Hooks Claude Code, échec de la {Action} : {Reason}", action, reason);
        return new HookSetupResult(false, FailurePrefix + reason);
    }
}
