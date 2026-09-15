using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

public readonly record struct MapRect(double X, double Y, double Width, double Height);

/// <summary><paramref name="Label"/> : description lisible (« Écran 2 — 1920 × 1080, 100 % »), la même que dans la liste des écrans.</summary>
public sealed record MonitorTile(string DeviceId, int Number, bool IsPrimary, bool IsSelected, MapRect Rect, string Label);

public sealed record MonitorMapModel(IReadOnlyList<MonitorTile> Tiles, MapRect? PillMarker)
{
    public static MonitorMapModel Empty { get; } = new([], null);
}

/// <summary>Miniature des écrans : le bureau virtuel réduit et centré dans une zone donnée, avec le repère de la pilule.</summary>
public static class MonitorMap
{
    /// <summary>Plus petite dimension du repère : visible même sur un bureau de nombreux écrans.</summary>
    public const double MinMarkerSize = 6;

    public static MonitorMapModel Layout(IReadOnlyList<MonitorInfo> monitors, CoreSettings settings, double width, double height, double padding)
    {
        if (monitors.Count == 0) return MonitorMapModel.Empty;

        var ordered = MonitorChoices.Ordered(monitors);
        var left = ordered.Min(m => m.Bounds.X);
        var top = ordered.Min(m => m.Bounds.Y);
        var desktopWidth = ordered.Max(m => m.Bounds.Right) - left;
        var desktopHeight = ordered.Max(m => m.Bounds.Bottom) - top;
        var factor = Math.Min((width - 2 * padding) / desktopWidth, (height - 2 * padding) / desktopHeight);
        var offsetX = (width - desktopWidth * factor) / 2;
        var offsetY = (height - desktopHeight * factor) / 2;

        MapRect Map(PixelRect r) => new(offsetX + (r.X - left) * factor, offsetY + (r.Y - top) * factor, r.Width * factor, r.Height * factor);

        var selected = PillPlacement.Choose(ordered, settings.MonitorDeviceId);
        var tiles = ordered
            .Select((m, i) => new MonitorTile(m.DeviceId, i + 1, m.IsPrimary, ReferenceEquals(m, selected), Map(m.Bounds), MonitorChoices.Describe(m, i + 1)))
            .ToList();

        var physical = selected.Scale * settings.Scale;
        var pill = PillPlacement.PillRect(
            selected.Bounds,
            settings.Edge,
            settings.PositionFor(settings.Edge),
            (int)Math.Round(PillMetrics.WindowLength * physical),
            (int)Math.Round(PillMetrics.Thickness * physical));

        return new MonitorMapModel(tiles, Enlarge(Map(pill), settings.Edge));
    }

    /// <summary>Un repère trop fin est élargi à <see cref="MinMarkerSize"/>, en restant collé au bord de l'écran.</summary>
    private static MapRect Enlarge(MapRect r, ScreenEdge edge)
    {
        if (r.Width < MinMarkerSize)
        {
            r = edge == ScreenEdge.Right
                ? r with { X = r.X + r.Width - MinMarkerSize, Width = MinMarkerSize }
                : r with { Width = MinMarkerSize };
        }
        if (r.Height < MinMarkerSize)
        {
            r = edge == ScreenEdge.Bottom
                ? r with { Y = r.Y + r.Height - MinMarkerSize, Height = MinMarkerSize }
                : r with { Height = MinMarkerSize };
        }
        return r;
    }
}
