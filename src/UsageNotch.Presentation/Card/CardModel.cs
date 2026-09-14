using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Card;

public sealed record WindowRow(string Label, string ResetText, double Fraction, string BarColor, string UsedText);

public sealed record SessionRow(string SessionId, string Title, string Detail, ActivityKind Activity, string DotColor);

public sealed record CardModel(
    string Title,
    string? Subtitle,
    IReadOnlyList<WindowRow> Windows,
    string? Note,
    IReadOnlyList<SessionRow> Sessions);
