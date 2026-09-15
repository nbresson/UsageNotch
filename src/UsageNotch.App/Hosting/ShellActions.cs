using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

public sealed class ShellActions(AppPaths paths, SettingsStore settings, ILogger<ShellActions> logger) : IShellActions
{
    public void OpenFolder(string path) => Start("explorer.exe", $"\"{path}\"");

    public void OpenFile(string path) => Start("notepad.exe", $"\"{path}\"");

    public string? RunDoctor()
    {
        DoctorOutput output;
        try
        {
            output = DoctorCommand.WriteReport(paths, settings);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Diagnostic impossible");
            return "Diagnostic impossible : " + e.Message;
        }

        if (output.FilePath is null) return $"Le diagnostic n'a pas pu être écrit dans {paths.LogsDirectory}.";
        OpenFile(output.FilePath);
        return null;
    }

    private void Start(string file, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false })?.Dispose();
        }
        catch (Win32Exception e)
        {
            logger.LogWarning(e, "Impossible de lancer {File}", file);
        }
    }
}
