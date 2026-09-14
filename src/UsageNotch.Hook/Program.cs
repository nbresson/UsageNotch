using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UsageNotch.Hook;

/// <summary>
/// Appelé par les hooks de Claude Code : relaie l'événement (argument) et le JSON (stdin) à l'app.
/// Règle absolue : ne jamais bloquer Claude Code — budget ~2 s, toute erreur sort en 0 sans bruit.
/// </summary>
internal static class Program
{
    private const int MaxStdinBytes = 256 * 1024;
    private const int ConnectTimeoutMs = 300;
    private const int IoTimeoutMs = 700;
    private const string AppExeName = "UsageNotch.App.exe";

    private static int Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch
        {
            // Silence : le hook ne doit jamais faire échouer Claude Code.
        }
        return 0;
    }

    private static void Run(string[] args)
    {
        var kind = args.Length > 0 ? args[0] : "ping";
        var body = ReadStdin();
        var port = PortReader.Read(ReadSettings());
        var ppid = ParentProcess.Id();

        if (Send(port, kind, ppid, body)) return;

        // L'app ne répond pas : la lancer détachée puis réessayer brièvement.
        LaunchApp();
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(100);
            if (Send(port, kind, ppid, body)) return;
        }
    }

    private static string ReadStdin()
    {
        if (!Console.IsInputRedirected) return "";
        using var input = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= MaxStdinBytes) break;
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)Math.Min(buffer.Length, MaxStdinBytes));
    }

    private static string? ReadSettings()
    {
        try
        {
            var path = PortReader.DefaultSettingsPath;
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool Send(int port, string kind, int ppid, string body)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(IPAddress.Loopback, port);
            if (!connect.Wait(ConnectTimeoutMs) || !client.Connected) return false;
            client.SendTimeout = IoTimeoutMs;
            client.ReceiveTimeout = IoTimeoutMs;

            using var stream = client.GetStream();
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var head = $"POST /event?e={Uri.EscapeDataString(kind)}&ppid={ppid} HTTP/1.1\r\n"
                     + "Host: 127.0.0.1\r\nContent-Type: application/json\r\n"
                     + $"Content-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
            stream.Write(Encoding.ASCII.GetBytes(head));
            stream.Write(bodyBytes);
            stream.Flush();

            var ack = new byte[64];
            _ = stream.Read(ack, 0, ack.Length); // un fragment de réponse confirme la livraison ; l'échec n'a pas d'importance
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LaunchApp()
    {
        try
        {
            var exe = Path.Combine(AppContext.BaseDirectory, AppExeName);
            if (!File.Exists(exe)) return;
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            Process.Start(start);
        }
        catch
        {
            // L'app absente ou impossible à lancer : on abandonne en silence.
        }
    }
}
