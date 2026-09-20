namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Tout ce que la cellule dessine. <see cref="Rings"/> porte toujours trois anneaux, de l'extérieur vers
/// l'intérieur. Couleurs en <c>#RRGGBB</c>. <see cref="ActivityMutedColor"/> est la version grise que croise
/// la pulsation de l'état « en attente ».
/// </summary>
public sealed record CellModel(
    IReadOnlyList<RingModel> Rings,
    string TrackColor,
    string PercentText,
    string TextColor,
    bool ShowPercent,
    bool Dimmed,
    bool Exhausted,
    ActivityKind Activity,
    string ActivityColor,
    string ActivityMutedColor,
    string BandColor);
