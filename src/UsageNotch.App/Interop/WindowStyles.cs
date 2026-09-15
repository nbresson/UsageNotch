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

    /// <summary>
    /// À appeler pour chaque message d'une fenêtre non activable. Quand elle passe sur un écran d'une autre mise à
    /// l'échelle, WPF la repositionne lui-même sans SWP_NOACTIVATE et Windows l'active : elle volerait alors le premier
    /// plan et le focus (par exemple à la fenêtre de réglages). L'activation est aussitôt rendue à la fenêtre qui l'avait.
    /// </summary>
    public static void ReturnActivation(int msg, nint wParam, nint lParam, System.Windows.Threading.Dispatcher dispatcher)
    {
        if (msg != NativeMethods.WM_ACTIVATE || (wParam.ToInt64() & 0xFFFF) == NativeMethods.WA_INACTIVE || lParam == 0) return;
        var previous = lParam;
        dispatcher.BeginInvoke(new Action(() => NativeMethods.SetForegroundWindow(previous)));
    }
}
