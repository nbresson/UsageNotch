using System.Windows;
using System.Windows.Media;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Controls;

/// <summary>
/// Formes en DIP, dans le repère de la fenêtre. On les décrit dans un repère canonique (x : de l'intérieur de l'écran
/// vers le bord, 0 → épaisseur ; y : le long du bord, 0 → longueur, congés compris) puis on les projette selon le bord.
/// </summary>
public static class PillShapeBuilder
{
    /// <summary>
    /// Corps aux coins arrondis côté intérieur, plat côté bord, prolongé jusqu'au bord par deux congés concaves.
    /// Canonique : (T,0) → (T,L) le long du bord → congé → coin → côté intérieur → coin → congé → (T,0).
    /// </summary>
    public static Geometry Pill(ScreenEdge edge, double thickness, double length, double radius, double fillet)
    {
        var t = thickness;
        var l = length;
        var r = radius;
        var f = fillet;
        var flips = Flips(edge);
        var convex = flips ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
        var concave = flips ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        Point P(double x, double y) => Map(edge, x, y, t);

        var figure = new PathFigure { StartPoint = P(t, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(P(t, l), isStroked: true));
        figure.Segments.Add(new ArcSegment(P(t - f, l - f), new Size(f, f), 0, false, concave, true));
        figure.Segments.Add(new LineSegment(P(r, l - f), true));
        figure.Segments.Add(new ArcSegment(P(0, l - f - r), new Size(r, r), 0, false, convex, true));
        figure.Segments.Add(new LineSegment(P(0, f + r), true));
        figure.Segments.Add(new ArcSegment(P(r, f), new Size(r, r), 0, false, convex, true));
        figure.Segments.Add(new LineSegment(P(t - f, f), true));
        figure.Segments.Add(new ArcSegment(P(t, 0), new Size(f, f), 0, false, concave, true));

        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>La bande du mode Replié : collée au bord, sur la longueur du corps (congés exclus).</summary>
    public static Rect Band(ScreenEdge edge, double thickness, double length, double band, double fillet) =>
        new(Map(edge, thickness - band, fillet, thickness), Map(edge, thickness, length - fillet, thickness));

    /// <summary>Zone du contenu (anneau et pourcentage) : toute l'épaisseur, entre les deux congés.</summary>
    public static Rect Body(ScreenEdge edge, double thickness, double length, double fillet) =>
        new(Map(edge, 0, fillet, thickness), Map(edge, thickness, length - fillet, thickness));

    /// <summary>Déplacement qui pousse la pilule entièrement derrière le bord de l'écran.</summary>
    public static Vector SlideOffset(ScreenEdge edge, double thickness) => edge switch
    {
        ScreenEdge.Left => new Vector(-thickness, 0),
        ScreenEdge.Top => new Vector(0, -thickness),
        ScreenEdge.Bottom => new Vector(0, thickness),
        _ => new Vector(thickness, 0),
    };

    private static Point Map(ScreenEdge edge, double x, double y, double thickness) => edge switch
    {
        ScreenEdge.Left => new Point(thickness - x, y),
        ScreenEdge.Top => new Point(y, thickness - x),
        ScreenEdge.Bottom => new Point(y, x),
        _ => new Point(x, y),
    };

    /// <summary>Les projections gauche et bas sont des symétries : le sens des arcs s'inverse.</summary>
    private static bool Flips(ScreenEdge edge) => edge is ScreenEdge.Left or ScreenEdge.Bottom;
}
