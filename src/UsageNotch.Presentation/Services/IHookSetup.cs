namespace UsageNotch.Presentation.Services;

public sealed record HookSetupResult(bool Succeeded, string Message);

/// <summary>Installation des hooks dans settings.json de Claude Code. Ne lève pas : les échecs deviennent des messages.</summary>
public interface IHookSetup
{
    string SettingsPath { get; }
    string HookExePath { get; }
    bool HookExeExists { get; }
    bool IsInstalled();
    HookSetupResult Install();
    HookSetupResult Uninstall();
}
