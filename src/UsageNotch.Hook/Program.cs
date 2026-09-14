using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UsageNotch.Hook;

/// <summary>
/// Appelé par les hooks de Claude Code : relaie l'événement (argument) et le JSON (stdin) à l'app.
/// Règle absolue : ne jamais bloquer Claude Code — délai global (<see cref="Budget"/>) de 2 s pour
/// l'ensemble de la tentative (lecture, connexion, réessais), toute erreur sort en 0 sans bruit.
/// </summary>
internal static class Program
{
    private const int MaxStdinBytes = 256 * 1024;
    private const int IoTimeoutMs = 700;
    private const string AppExeName = "UsageNotch.App.exe";
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan StdinWait = TimeSpan.FromSeconds(1);

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
        var sw = Stopwatch.StartNew();
        var kind = args.Length > 0 ? args[0] : "ping";
        var body = ReadStdin();
        var settings = ReadSettings();
        var port = PortReader.Read(settings);
        var autoLaunch = PortReader.ReadAutoLaunch(settings);
        var ppid = AncestorPicker.Pick(ParentProcess.Ancestors());
        if (ppid == 0) ppid = ParentProcess.Id();

        var remaining = Budget - sw.Elapsed;
        if (remaining <= TimeSpan.Zero) return;

        if (Send(port, kind, ppid, body, Min(ConnectTimeout, remaining), sw)) return;

        // L'utilisateur a quitté l'app (autoLaunch = false) : ni lancement ni réessai.
        if (!autoLaunch) return;

        // L'app ne répond pas : la lancer détachée puis réessayer jusqu'à épuisement du budget.
        LaunchApp();
        while (true)
        {
            remaining = Budget - sw.Elapsed;
            if (remaining <= TimeSpan.FromMilliseconds(100)) break;
            Thread.Sleep(100);
            remaining = Budget - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            if (Send(port, kind, ppid, body, Min(ConnectTimeout, remaining), sw)) return;
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static string ReadStdin()
    {
        if (!Console.IsInputRedirected) return "";

        // Lu sur un thread d'arrière-plan avec un délai borné : si l'appelant garde stdin ouvert sans
        // EOF, on ne doit pas bloquer indéfiniment. Un thread d'arrière-plan encore coincé dans Read
        // ne retient pas le processus vivant une fois Main revenu.
        var buffer = new MemoryStream();
        var thread = new Thread(() =>
        {
            try
            {
                using var input = Console.OpenStandardInput();
                var chunk = new byte[16 * 1024];
                int read;
                while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
                {
                    bool full;
                    lock (buffer)
                    {
                        buffer.Write(chunk, 0, read);
                        full = buffer.Length >= MaxStdinBytes;
                    }
                    if (full) break;
                }
            }
            catch
            {
                // Ignoré : on utilisera ce qui a déjà été lu.
            }
        })
        { IsBackground = true };
        thread.Start();
        thread.Join(StdinWait);

        lock (buffer)
        {
            var length = (int)Math.Min(buffer.Length, MaxStdinBytes);
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, length);
        }
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

    private static bool Send(int port, string kind, int ppid, string body, TimeSpan connectTimeout, Stopwatch sw)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(IPAddress.Loopback, port);
            if (!connect.Wait(connectTimeout) || !client.Connected) return false;

            // Le budget total peut être presque épuisé après la connexion : borner l'IO par ce qu'il
            // en reste, avec un plancher de 1 ms (0 signifierait "infini" pour ces propriétés).
            var remainingMs = (Budget - sw.Elapsed).TotalMilliseconds;
            if (remainingMs <= 0) return false;
            var ioTimeout = Math.Max(1, (int)Math.Min(IoTimeoutMs, remainingMs));
            client.SendTimeout = ioTimeout;
            client.ReceiveTimeout = ioTimeout;

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
            // UseShellExecute = true : ShellExecuteEx ne transmet pas les handles hérités, donc l'app ne garde
            // pas les pipes stdio que Claude Code a donnés au hook (sinon Claude Code attendrait la fin de l'app).
            // --from-hook : une seconde instance lancée ainsi se ferme sans rien demander à la première.
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Arguments = "--from-hook",
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
