using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Placement;

/// <summary>Géométrie pure : où va la pilule sur un bord, où va la carte face à une cellule. Aucune dépendance système.</summary>
public static class PillPlacement
{
    public static bool IsVertical(ScreenEdge edge) => edge is ScreenEdge.Right or ScreenEdge.Left;

    /// <summary>La pilule collée au bord physique de l'écran, glissée le long du bord selon <paramref name="fraction"/> (0 = début, 1 = fin).</summary>
    public static PixelRect PillRect(PixelRect monitor, ScreenEdge edge, double fraction, int length, int thickness)
    {
        var f = Math.Clamp(fraction, 0.0, 1.0);
        if (IsVertical(edge))
        {
            var travel = Math.Max(0, monitor.Height - length);
            var y = monitor.Y + (int)Math.Round(travel * f);
            var x = edge == ScreenEdge.Right ? monitor.Right - thickness : monitor.X;
            return new PixelRect(x, y, thickness, length);
        }
        else
        {
            var travel = Math.Max(0, monitor.Width - length);
            var x = monitor.X + (int)Math.Round(travel * f);
            var y = edge == ScreenEdge.Bottom ? monitor.Bottom - thickness : monitor.Y;
            return new PixelRect(x, y, length, thickness);
        }
    }

    /// <summary>Inverse de <see cref="PillRect"/>, pour mémoriser la position après un glisser.</summary>
    public static double FractionOf(PixelRect monitor, ScreenEdge edge, PixelRect pill)
    {
        if (IsVertical(edge))
        {
            var travel = monitor.Height - pill.Height;
            return travel <= 0 ? 0.0 : Math.Clamp((pill.Y - monitor.Y) / (double)travel, 0.0, 1.0);
        }
        else
        {
            var travel = monitor.Width - pill.Width;
            return travel <= 0 ? 0.0 : Math.Clamp((pill.X - monitor.X) / (double)travel, 0.0, 1.0);
        }
    }

    /// <summary>La carte face à <paramref name="anchor"/>, vers le centre de l'écran, maintenue dans l'écran à <paramref name="margin"/> près.</summary>
    public static PixelRect CardRect(PixelRect monitor, ScreenEdge edge, PixelRect pill, PixelRect anchor, int cardWidth, int cardHeight, int gap, int margin)
    {
        int x, y;
        switch (edge)
        {
            case ScreenEdge.Right:
                x = pill.X - gap - cardWidth;
                y = anchor.CenterY - cardHeight / 2;
                break;
            case ScreenEdge.Left:
                x = pill.Right + gap;
                y = anchor.CenterY - cardHeight / 2;
                break;
            case ScreenEdge.Top:
                x = anchor.CenterX - cardWidth / 2;
                y = pill.Bottom + gap;
                break;
            default:
                x = anchor.CenterX - cardWidth / 2;
                y = pill.Y - gap - cardHeight;
                break;
        }

        x = Math.Clamp(x, monitor.X + margin, Math.Max(monitor.X + margin, monitor.Right - margin - cardWidth));
        y = Math.Clamp(y, monitor.Y + margin, Math.Max(monitor.Y + margin, monitor.Bottom - margin - cardHeight));
        return new PixelRect(x, y, cardWidth, cardHeight);
    }

    /// <summary>L'écran mémorisé s'il est présent, sinon le principal, sinon le premier. Le choix n'est jamais perdu : l'appelant garde l'identifiant.</summary>
    public static MonitorInfo Choose(IReadOnlyList<MonitorInfo> monitors, string? preferredDeviceId)
    {
        if (monitors.Count == 0) throw new InvalidOperationException("Aucun écran détecté");
        if (preferredDeviceId is not null)
        {
            var preferred = monitors.FirstOrDefault(m => m.DeviceId.Equals(preferredDeviceId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null) return preferred;
        }
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }
}
