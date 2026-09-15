using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.App.Interop;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Diagnostics;

namespace UsageNotch.App.Hosting;

/// <summary><paramref name="FilePath"/> : chemin de doctor.txt, ou null si l'écriture a échoué.</summary>
public sealed record DoctorOutput(string Text, string? FilePath);

public static class DoctorCommand
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary><c>UsageNotch.App.exe doctor</c> : écrit le rapport puis l'affiche sur la console qui a lancé l'application.</summary>
    public static string Run(AppPaths paths, SettingsStore settings)
    {
        var output = WriteReport(paths, settings);
        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), Utf8) { AutoFlush = true };
        stdout.Write(output.Text);
        return output.Text;
    }

    /// <summary>Construit le rapport et l'écrit dans logs\doctor.txt, sans console : utilisable depuis l'interface.</summary>
    public static DoctorOutput WriteReport(AppPaths paths, SettingsStore settings)
    {
        var time = TimeProvider.System;
        var credentials = new ClaudeCredentialReader(ClaudeCredentialReader.DefaultDirectory, time);
        var installer = new HookInstaller(paths.ClaudeSettingsFile, paths.HookExe, time);
        var usage = new UsageStore(paths.UsageFile, time, NullLogger<UsageStore>.Instance);
        usage.Load();

        var port = settings.Current.Port;
        bool portFree;
        try
        {
            var probe = new TcpListener(IPAddress.Loopback, port);
            probe.Start();
            probe.Stop();
            portFree = true;
        }
        catch (SocketException)
        {
            portFree = false;
        }

        var text = DoctorReport.Build(new DoctorInputs(
            CredentialsDirectory: credentials.Directory,
            Credential: credentials.Read(),
            ClaudeSettingsPath: installer.SettingsPath,
            HooksInstalled: installer.IsInstalled(),
            HookExePath: paths.HookExe,
            HookExeExists: File.Exists(paths.HookExe),
            Port: port,
            PortFree: portFree,
            Monitors: new MonitorService().GetMonitors(),
            UsagePath: paths.UsageFile,
            Usage: usage.Current,
            SettingsPath: paths.SettingsFile,
            Settings: settings.Current,
            Now: time.GetUtcNow(),
            Zone: TimeZoneInfo.Local));

        var file = Path.Combine(paths.LogsDirectory, "doctor.txt");
        try
        {
            Directory.CreateDirectory(paths.LogsDirectory);
            File.WriteAllText(file, text, Utf8);
            return new DoctorOutput(text, file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Le rapport reste affichable sur la console.
            return new DoctorOutput(text, null);
        }
    }
}
