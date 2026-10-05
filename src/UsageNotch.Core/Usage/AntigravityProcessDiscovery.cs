using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace UsageNotch.Core.Usage;

public sealed record AntigravityHubInfo(int Port, string CsrfToken);

public interface IAntigravityProcessInspector
{
    IEnumerable<string> GetCandidateCommandLines();
}

public sealed partial class AntigravityProcessDiscovery(IAntigravityProcessInspector? inspector = null)
{
    private readonly IAntigravityProcessInspector _inspector = inspector ?? new WindowsAntigravityProcessInspector();
    private readonly object _lock = new();
    private AntigravityHubInfo? _cached;

    [GeneratedRegex(@"--hub-port[=:]\s*[""']?(\d+)[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex PortRegex();

    [GeneratedRegex(@"--csrf_token[=:]\s*[""']?([^\s""']+)[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex CsrfRegex();

    public AntigravityHubInfo? Discover()
    {
        lock (_lock)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            foreach (var line in _inspector.GetCandidateCommandLines())
            {
                var portMatch = PortRegex().Match(line);
                var csrfMatch = CsrfRegex().Match(line);

                if (portMatch.Success
                    && csrfMatch.Success
                    && int.TryParse(portMatch.Groups[1].Value, out var port))
                {
                    _cached = new AntigravityHubInfo(port, csrfMatch.Groups[1].Value);
                    return _cached;
                }
            }

            return null;
        }
    }

    public void InvalidateCache()
    {
        lock (_lock)
        {
            _cached = null;
        }
    }
}

internal sealed class WindowsAntigravityProcessInspector : IAntigravityProcessInspector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public nint Buffer;
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(nint hProcess, int infoClass, nint buffer, int size, out int retSize);

    public IEnumerable<string> GetCandidateCommandLines()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("agy");
        }
        catch
        {
            yield break;
        }

        foreach (var p in processes)
        {
            string? cmd = null;
            try
            {
                cmd = GetCommandLine(p.Id);
            }
            catch
            {
                // Processus inaccessible ou terminé
            }
            finally
            {
                p.Dispose();
            }

            if (!string.IsNullOrEmpty(cmd))
            {
                yield return cmd;
            }
        }
    }

    private static string? GetCommandLine(int pid)
    {
        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == 0) return null;
        try
        {
            const int size = 4096;
            var buf = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQueryInformationProcess(h, ProcessCommandLineInformation, buf, size, out _);
                if (status == 0)
                {
                    var u = Marshal.PtrToStructure<UnicodeString>(buf);
                    if (u.Buffer != 0 && u.Length > 0)
                    {
                        return Marshal.PtrToStringUni(u.Buffer, u.Length / 2);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            CloseHandle(h);
        }
        return null;
    }
}
