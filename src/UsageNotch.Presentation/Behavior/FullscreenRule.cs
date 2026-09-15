using UsageNotch.Core.Placement;

namespace UsageNotch.Presentation.Behavior;

/// <summary>La fenêtre au premier plan telle que l'App la lit (rectangle en pixels physiques, style, classe).</summary>
public sealed record ForegroundWindow(
    PixelRect Bounds,
    bool HasCaption,
    bool IsMinimized,
    bool IsVisible,
    bool IsOwnProcess,
    string ClassName);

/// <summary>
/// Une fenêtre est en plein écran sur un écran quand elle le couvre entièrement, barre des tâches comprise, sans barre de
/// titre (une fenêtre simplement agrandie en garde une). Le bureau, la barre des tâches et nos propres fenêtres ne
/// comptent jamais.
/// </summary>
public static class FullscreenRule
{
    private static readonly string[] ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    public static bool IsFullscreenOn(ForegroundWindow? window, PixelRect monitor) =>
        window is { IsVisible: true, IsMinimized: false, IsOwnProcess: false, HasCaption: false }
        && !ShellClasses.Contains(window.ClassName, StringComparer.Ordinal)
        && window.Bounds.X <= monitor.X
        && window.Bounds.Y <= monitor.Y
        && window.Bounds.Right >= monitor.Right
        && window.Bounds.Bottom >= monitor.Bottom;
}
