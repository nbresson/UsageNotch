namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Tout ce que la cellule dessine. <see cref="RingFraction"/> null = pas de lecture exploitable (tiret ou attente).
/// Couleurs en <c>#RRGGBB</c>.
/// </summary>
public sealed record CellModel(
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
