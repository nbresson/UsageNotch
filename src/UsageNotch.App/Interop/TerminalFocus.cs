using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>
/// Ramène au premier plan la fenêtre qui héberge la session et fait clignoter son bouton de barre des tâches.
/// Le clic de l'utilisateur sur la carte donne à UsageNotch le droit de changer la fenêtre active.
/// </summary>
public sealed class TerminalFocus(ILogger<TerminalFocus> logger) : ISessionFocus
{
    public bool Focus(int? parentPid, DateTimeOffset sessionStarted)
    {
        if (parentPid is not int pid || pid <= 0) return false;
        if (StartTimeOf(pid) is { } started && !TerminalWindowChooser.CanHostSession(started, sessionStarted))
        {
            logger.LogDebug("Le processus {Pid} a démarré après la session : numéro réutilisé, rien n'est ramené", pid);
            return false;
        }

        var handle = TerminalWindowChooser.Choose(pid, ProcessParents(), VisibleTitledWindows(), Environment.ProcessId);
        if (handle is not nint hwnd)
        {
            logger.LogDebug("Aucune fenêtre trouvée pour le processus {Pid}", pid);
            return false;
        }

        if (NativeMethods.IsIconic(hwnd)) NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        var flash = new NativeMethods.FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG,
            uCount = 3,
            dwTimeout = 0,
        };
        NativeMethods.FlashWindowEx(ref flash);
        return true;
    }

    /// <summary>Heure de démarrage, ou null si elle est illisible (processus terminé, accès refusé).</summary>
    private static DateTimeOffset? StartTimeOf(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return new DateTimeOffset(process.StartTime);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static Dictionary<int, int> ProcessParents()
    {
        var parents = new Dictionary<int, int>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
        if (snapshot == NativeMethods.INVALID_HANDLE_VALUE || snapshot == 0) return parents;
        try
        {
            var entry = new NativeMethods.PROCESSENTRY32W
            {
                dwSize = (uint)Marshal.SizeOf<NativeMethods.PROCESSENTRY32W>(),
                szExeFile = "",
            };
            if (!NativeMethods.Process32FirstW(snapshot, ref entry)) return parents;
            do
            {
                parents[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
            }
            while (NativeMethods.Process32NextW(snapshot, ref entry));
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
        return parents;
    }

    private static List<TopLevelWindow> VisibleTitledWindows()
    {
        var windows = new List<TopLevelWindow>();
        NativeMethods.EnumWindowsProc callback = (hwnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hwnd) && NativeMethods.GetWindowTextLengthW(hwnd) > 0)
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
                windows.Add(new TopLevelWindow(hwnd, (int)processId));
            }
            return true;
        };
        NativeMethods.EnumWindows(callback, 0);
        GC.KeepAlive(callback);
        return windows;
    }
}
