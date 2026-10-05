using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Card;

public sealed record WindowRow(string Label, string ResetText, double Fraction, string BarColor, string UsedText);

public sealed record SessionRow(string SessionId, string Title, string Detail, ActivityKind Activity, string DotColor);

public sealed record CardSection(
    string ProviderId,
    string Title,
    string? Subtitle,
    IReadOnlyList<WindowRow> Windows,
    string? Note);

public sealed record CardModel(
    IReadOnlyList<CardSection> Sections,
    IReadOnlyList<SessionRow> Sessions,
    string? HeaderTitle = null)
{
    public string Title => Sections.Count > 0 ? Sections[0].Title : "";
    public string? Subtitle => Sections.Count > 0 ? Sections[0].Subtitle : null;
    public IReadOnlyList<WindowRow> Windows => Sections.Count > 0 ? Sections[0].Windows : [];
    public string? Note => Sections.Count > 0 ? Sections[0].Note : null;
    public bool HasSessions => Sessions.Count > 0;

    /// <summary>Constructeur de rétro-compatibilité pour une carte à section unique.</summary>
    public CardModel(
        string title,
        string? subtitle,
        IReadOnlyList<WindowRow> windows,
        string? note,
        IReadOnlyList<SessionRow> sessions)
        : this([new CardSection("claude", title, subtitle, windows, note)], sessions, null)
    {
    }
}
