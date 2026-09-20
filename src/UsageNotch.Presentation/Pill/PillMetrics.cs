namespace UsageNotch.Presentation.Pill;

/// <summary>Dimensions logiques (DIP) à l'échelle 100 %. L'App les multiplie par Settings.Scale.</summary>
public static class PillMetrics
{
    public const double Thickness = 64;
    public const double BodyLength = 104;
    public const double CornerRadius = 16;
    public const double Fillet = 16;
    /// <summary>Diamètres de la pile, de l'extérieur vers l'intérieur. L'hôte de la pile vaut <see cref="RingOuter"/>.</summary>
    public const double RingOuter = 56;
    public const double RingMiddle = 44;
    public const double RingInner = 32;
    public const double RingBandThickness = 4;
    /// <summary>Côté du glyphe de marque, au centre de la pile, dans un trou de 24 DIP.</summary>
    public const double LogoSize = 20;
    public const double CardGap = 10;
    public const double ScreenMargin = 8;

    /// <summary>Longueur de la fenêtre le long du bord : le corps plus les deux congés qui le soudent au bord.</summary>
    public static double WindowLength => BodyLength + 2 * Fillet;
}
