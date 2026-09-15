using System.Runtime.InteropServices;
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Énumère les écrans en pixels physiques, avec leur facteur d'échelle effectif (1,5 = 150 %).</summary>
public sealed class MonitorService : IMonitorSource
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        NativeMethods.MonitorEnumProc callback = (nint hMonitor, nint hdc, ref NativeMethods.RECT rect, nint data) =>
        {
            var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(), szDevice = "" };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info)) return true;

            var scale = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
                ? dpiX / 96.0
                : 1.0;

            list.Add(new MonitorInfo(
                info.szDevice,
                (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                ToPixelRect(info.rcMonitor),
                ToPixelRect(info.rcWork),
                scale));
            return true;
        };

        NativeMethods.EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        return list;
    }

    private static PixelRect ToPixelRect(NativeMethods.RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
