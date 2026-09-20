namespace UsageNotch.Presentation.Pill;

/// <summary>Dimensions logiques (DIP) à l'échelle 100 %. L'App les multiplie par Settings.Scale.</summary>
public static class PillMetrics
{
    public const double Thickness = 64;
    public const double BodyLength = 104;
    public const double CornerRadius = 16;
    public const double Fillet = 16;
    /// <summary>Hôte de la pile : contient les trois anneaux et les voyants d'activité qui les entourent.</summary>
    public const double RingHostSize = 56;
    public const double RingOuter = 44;
    public const double RingMiddle = 32;
    public const double RingInner = 20;
    public const double RingBandThickness = 4;
    /// <summary>Diamètre de l'arc de rotation et de l'anneau d'attente, tracés autour de la pile.</summary>
    public const double ActivitySize = 52;
    public const double CardGap = 10;
    public const double ScreenMargin = 8;

    /// <summary>Longueur de la fenêtre le long du bord : le corps plus les deux congés qui le soudent au bord.</summary>
    public static double WindowLength => BodyLength + 2 * Fillet;
}
