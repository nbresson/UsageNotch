namespace UsageNotch.Presentation.Pill;

/// <summary>
/// Ce qu'un anneau dessine. <see cref="Fraction"/> null = pas de lecture exploitable : la piste seule.
/// Couleurs en <c>#RRGGBB</c>.
/// </summary>
public sealed record RingModel(double? Fraction, string Color, string TrackColor);
