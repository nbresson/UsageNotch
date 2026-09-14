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

public static class DoctorCommand
{
    public static string Run(AppPaths paths, SettingsStore settings)
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

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            Directory.CreateDirectory(paths.LogsDirectory);
            File.WriteAllText(Path.Combine(paths.LogsDirectory, "doctor.txt"), text, utf8);
        }
        catch (IOException)
        {
            // Le rapport s'affiche quand même sur la console.
        }

        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
        stdout.Write(text);
        return text;
    }
}
