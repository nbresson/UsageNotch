using System.Text;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Interop;

/// <summary>
/// Suit la fenêtre au premier plan par les événements Windows (changement de premier plan, réduction, déplacement ou
/// redimensionnement) et signale au ViewModel quand elle est en plein écran sur l'écran de la pilule. Aucun sondage :
/// rien ne tourne au repos. Crochets hors contexte installés et reçus sur le thread UI.
/// </summary>
public sealed class FullscreenWatcher(NotchViewModel viewModel, ILogger<FullscreenWatcher> logger) : IDisposable
{
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private readonly List<nint> _hooks = [];
    private NativeMethods.WinEventProc? _callback;
    private Func<PixelRect?>? _pillMonitor;

    /// <param name="pillMonitor">Rectangle physique de l'écran de la pilule, ou null si la pilule n'a jamais été placée.</param>
    public void Start(Func<PixelRect?> pillMonitor)
    {
        if (_callback is not null) return;
        _pillMonitor = pillMonitor;
        // Le délégué est gardé dans un champ : le ramasse-miettes ne doit pas le libérer tant que les crochets existent.
        _callback = OnWinEvent;
        Hook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND);
        Hook(NativeMethods.EVENT_SYSTEM_MINIMIZESTART, NativeMethods.EVENT_SYSTEM_MINIMIZEEND);
        Hook(NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, NativeMethods.EVENT_OBJECT_LOCATIONCHANGE);
        Evaluate();
    }

    /// <summary>Relit la fenêtre au premier plan ; aussi appelé quand la pilule change d'écran.</summary>
    public void Evaluate()
    {
        if (_pillMonitor?.Invoke() is not { } monitor)
        {
            viewModel.SetForegroundFullscreen(false);
            return;
        }
        viewModel.SetForegroundFullscreen(FullscreenRule.IsFullscreenOn(Read(NativeMethods.GetForegroundWindow()), monitor));
    }

    private void Hook(uint min, uint max)
    {
        var hook = NativeMethods.SetWinEventHook(min, max, 0, _callback!, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        if (hook == 0) logger.LogWarning("Crochet d'événements Windows {Min:X}-{Max:X} impossible : le masquage en plein écran sera incomplet", min, max);
        else _hooks.Add(hook);
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Les déplacements concernent toutes les fenêtres et le curseur : seule la fenêtre au premier plan compte.
        if (eventType == NativeMethods.EVENT_OBJECT_LOCATIONCHANGE
            && (idObject != NativeMethods.OBJID_WINDOW || hwnd != NativeMethods.GetForegroundWindow()))
        {
            return;
        }
        Evaluate();
    }

    private ForegroundWindow? Read(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.GetWindowRect(hwnd, out var rect)) return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);

        return new ForegroundWindow(
            Bounds: new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            HasCaption: (style & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION,
            IsMinimized: NativeMethods.IsIconic(hwnd),
            IsVisible: NativeMethods.IsWindowVisible(hwnd),
            IsOwnProcess: processId == _ownProcessId,
            ClassName: className.ToString());
    }

    public void Dispose()
    {
        foreach (var hook in _hooks) NativeMethods.UnhookWinEvent(hook);
        _hooks.Clear();
        _callback = null;
    }
}
