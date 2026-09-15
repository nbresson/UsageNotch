using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests.Preferences;

public sealed class FakeColorPicker : IColorPicker
{
    public string? Result { get; set; }
    public List<string> Requests { get; } = [];

    public string? Pick(string initialHex)
    {
        Requests.Add(initialHex);
        return Result;
    }
}

public sealed class FakeAccent : IAccentColorSource
{
    public string? AccentHex { get; set; }
}

public sealed class FakeMonitors : IMonitorSource
{
    public List<MonitorInfo> Monitors { get; } = [];
    public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors.ToList();
}

public sealed class FakeSound : ISoundPlayer
{
    public List<string> Played { get; } = [];
    public void Play(string soundName) => Played.Add(soundName);
}

public sealed class FakeAutoStart : IAutoStart
{
    public bool IsAvailable { get; set; } = true;
    public bool Enabled { get; set; }
    public string? Error { get; set; }
    public int Reads { get; private set; }
    public List<bool> Writes { get; } = [];

    public bool IsEnabled()
    {
        Reads++;
        return Enabled;
    }

    public string? TrySet(bool enabled)
    {
        Writes.Add(enabled);
        if (Error is not null) return Error;
        Enabled = enabled;
        return null;
    }
}

public sealed class FakeHooks : IHookSetup
{
    public string SettingsPath { get; set; } = @"C:\Users\test\.claude\settings.json";
    public string HookExePath { get; set; } = @"C:\apps\UsageNotch\UsageNotch.Hook.exe";
    public bool HookExeExists { get; set; } = true;
    public bool Installed { get; set; }
    public HookSetupResult? NextResult { get; set; }

    public bool IsInstalled() => Installed;

    public HookSetupResult Install() => Apply(installed: true, "7 hooks écrits");

    public HookSetupResult Uninstall() => Apply(installed: false, "7 hooks retirés");

    private HookSetupResult Apply(bool installed, string message)
    {
        var result = NextResult ?? new HookSetupResult(true, message);
        if (result.Succeeded) Installed = installed;
        return result;
    }
}

public sealed class FakeShell : IShellActions
{
    public List<string> Folders { get; } = [];
    public List<string> Files { get; } = [];
    public int DoctorRuns { get; private set; }
    public string? DoctorError { get; set; }

    public void OpenFolder(string path) => Folders.Add(path);
    public void OpenFile(string path) => Files.Add(path);

    public string? RunDoctor()
    {
        DoctorRuns++;
        return DoctorError;
    }
}
