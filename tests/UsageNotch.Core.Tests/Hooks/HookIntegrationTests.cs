using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Hooks;

public class HookIntegrationTests : IAsyncLifetime
{
    private readonly SessionStore _sessions = new(TimeProvider.System);
    private HookListener _listener = null!;
    private int _port;

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async Task InitializeAsync()
    {
        _port = FreePort();
        _listener = new HookListener(_port, _sessions, NullLogger<HookListener>.Instance);
        await _listener.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _listener.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Raw_tcp_protocol_matching_hook_is_accepted_by_listener()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        using var stream = client.GetStream();

        var body = Encoding.UTF8.GetBytes("""{ "session_id": "raw-tcp-1", "cwd": "C:\\repo", "prompt": "testing" }""");
        var head = $"POST /event?e=running&ppid=9999 HTTP/1.1\r\n"
                 + "Host: 127.0.0.1\r\nContent-Type: application/json\r\n"
                 + "X-UsageNotch-Hook: 1\r\n"
                 + $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(body);
        await stream.FlushAsync();

        var responseBytes = new byte[256];
        var read = await stream.ReadAsync(responseBytes);
        var response = Encoding.ASCII.GetString(responseBytes, 0, read);

        response.Should().Contain("200 OK");
        response.Should().Contain("ok");

        var snapshot = _sessions.Snapshot();
        snapshot.Should().ContainSingle(s => s.Id == "raw-tcp-1" && s.State == SessionState.Running && s.ParentPid == 9999);
    }

    [Fact]
    public void Hook_binary_executes_and_delivers_event_via_process()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var hookDll = Path.Combine(solutionRoot, "src", "UsageNotch.Hook", "bin", "Debug", "net10.0", "UsageNotch.Hook.dll");

        if (!File.Exists(hookDll))
        {
            // Si le binaire n'est pas encore compilé en Debug, ignorer élégamment
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "UsageNotchHookTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsJson = $"{{\"port\": {_port}, \"autoLaunch\": false}}";
            File.WriteAllText(Path.Combine(tempDir, "settings.json"), settingsJson);

            var psi = new ProcessStartInfo("dotnet", $"\"{hookDll}\" running")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.Environment["USAGENOTCH_DATA_DIR"] = tempDir;

            using var proc = Process.Start(psi)!;
            proc.StandardInput.WriteLine("""{ "session_id": "proc-session-1", "cwd": "C:\\work", "prompt": "hook test" }""");
            proc.StandardInput.Close();

            var exited = proc.WaitForExit(3000);
            exited.Should().BeTrue();
            proc.ExitCode.Should().Be(0);

            var snapshot = _sessions.Snapshot();
            snapshot.Should().Contain(s => s.Id == "proc-session-1" && s.State == SessionState.Running);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
