using UsageNotch.App.Interop;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.App.Views;

public sealed record PlacementResult(MonitorInfo Monitor, PixelRect PillRect);

/// <summary>Traduit les réglages en rectangles physiques sur l'écran choisi (repli sur l'écran principal s'il est absent).</summary>
public sealed class NotchPlacer(MonitorService monitors)
{
    public PlacementResult Compute(Settings s)
    {
        var monitor = PillPlacement.Choose(monitors.GetMonitors(), s.MonitorDeviceId);
        var factor = s.Scale * monitor.Scale;
        var thickness = (int)Math.Round(PillMetrics.Thickness * factor);
        var length = (int)Math.Round(PillMetrics.WindowLength * factor);
        var rect = PillPlacement.PillRect(monitor.Bounds, s.Edge, s.PositionFor(s.Edge), length, thickness);
        return new PlacementResult(monitor, rect);
    }

    /// <summary>Position le long du bord qui centre la pilule sur le curseur.</summary>
    public double FractionForCursor(PlacementResult p, ScreenEdge edge, int cursorX, int cursorY)
    {
        var r = p.PillRect;
        var moved = PillPlacement.IsVertical(edge)
            ? r with { Y = cursorY - r.Height / 2 }
            : r with { X = cursorX - r.Width / 2 };
        return PillPlacement.FractionOf(p.Monitor.Bounds, edge, moved);
    }

    public PixelRect CardRect(PlacementResult p, Settings s, int cardWidthPx, int cardHeightPx)
    {
        var gap = (int)Math.Round(PillMetrics.CardGap * s.Scale * p.Monitor.Scale);
        var margin = (int)Math.Round(PillMetrics.ScreenMargin * p.Monitor.Scale);
        return PillPlacement.CardRect(p.Monitor.Bounds, s.Edge, p.PillRect, p.PillRect, cardWidthPx, cardHeightPx, gap, margin);
    }
}
