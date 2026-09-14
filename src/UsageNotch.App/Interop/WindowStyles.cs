using UsageNotch.Core.Placement;

namespace UsageNotch.App.Interop;

public static class WindowStyles
{
    /// <summary>La fenêtre ne prend jamais le focus et n'apparaît ni dans la barre des tâches ni dans Alt+Tab.</summary>
    public static void MakeToolWindowNoActivate(nint hwnd)
    {
        var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (nint)style);
    }

    /// <summary>Place et dimensionne en pixels physiques, au-dessus des autres fenêtres, sans activer.</summary>
    public static void MoveResize(nint hwnd, PixelRect rect) =>
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

    public static PixelRect? GetRect(nint hwnd) =>
        NativeMethods.GetWindowRect(hwnd, out var r)
            ? new PixelRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top)
            : null;

    public static (int X, int Y) CursorPosition() =>
        NativeMethods.GetCursorPos(out var p) ? (p.X, p.Y) : (int.MinValue, int.MinValue);

    public static bool IsAltDown() => NativeMethods.GetKeyState(NativeMethods.VK_MENU) < 0;
}
