namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Tout ce que la cellule dessine. <see cref="Rings"/> porte toujours trois anneaux, de l'extérieur vers
/// l'intérieur. <see cref="RingFraction"/> et <see cref="RingColor"/> reprennent l'anneau de session : ils
/// disparaissent dès que la vue lit <see cref="Rings"/>. Couleurs en <c>#RRGGBB</c>.
/// </summary>
public sealed record CellModel(
    IReadOnlyList<RingModel> Rings,
    double? RingFraction,
    string RingColor,
    string TrackColor,
    string PercentText,
    string TextColor,
    bool ShowRing,
    bool ShowPercent,
    bool Dimmed,
    bool Exhausted,
    ActivityKind Activity,
    string ActivityColor,
    string BandColor);
