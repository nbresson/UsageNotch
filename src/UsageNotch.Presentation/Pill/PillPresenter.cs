using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Pill;

public static class PillPresenter
{
    /// <summary>Une lecture Ok plus vieille que ça est assombrie : le planificateur relit toutes les 5 min au repos.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    public static CellModel Cell(
        UsageSnapshot snapshot,
        IReadOnlyList<IReadOnlyList<string>> ringWindowIds,
        SessionState aggregate,
        Theme theme,
        CellContent content,
        RingColoring coloring,
        DateTimeOffset now,
        string providerId = "claude")
    {
        var activity = ActivityOf(aggregate);
        var blind = snapshot.Status == SnapshotStatus.NeedsAuth;
        string[] fixedColors = [theme.RingSession, theme.RingWeeklyAll, theme.RingWeeklyScoped];

        var rings = new RingModel[ringWindowIds.Count];
        for (var i = 0; i < ringWindowIds.Count; i++)
        {
            var window = blind ? null : snapshot.Window(ringWindowIds[i]);
            double? fraction = window is null ? null : Math.Clamp(window.UsedFraction, 0.0, 1.0);
            rings[i] = new RingModel(fraction, ColorFor(fraction, fixedColors[i], theme, coloring), theme.RingTrack);
        }

        var session = rings[0].Fraction;
        var percent = session is { } p
            ? FrenchText.Percent(p)
            : IsWaitingForFirstReading(snapshot) ? "…" : "—";

        var dimmed = snapshot.Status == SnapshotStatus.Stale
            || (snapshot.FetchedAt != DateTimeOffset.MinValue && now - snapshot.FetchedAt > StaleAfter);

        var activityColor = ActivityColor(activity, theme, providerId);

        return new CellModel(
            Rings: rings,
            TrackColor: theme.RingTrack,
            PercentText: percent,
            TextColor: theme.Text,
            ShowPercent: content != CellContent.RingOnly,
            Dimmed: dimmed,
            Exhausted: session >= 1.0,
            Activity: activity,
            ActivityColor: activityColor,
            ActivityMutedColor: HexColor.Desaturate(activityColor),
            BandColor: activity == ActivityKind.Attention ? theme.Attention : rings[0].Color,
            ProviderId: providerId);
    }

    /// <summary>Sans lecture, l'anneau prend la couleur de sa piste : il disparaît dedans.</summary>
    private static string ColorFor(double? fraction, string fixedColor, Theme theme, RingColoring coloring) =>
        fraction is not { } f ? theme.RingTrack
        : coloring == RingColoring.ByLevel ? theme.LevelColor(f)
        : fixedColor;

    public static ActivityKind ActivityOf(SessionState state) => state switch
    {
        SessionState.Attention => ActivityKind.Attention,
        SessionState.Running => ActivityKind.Running,
        SessionState.Done => ActivityKind.Done,
        _ => ActivityKind.None,
    };

    public static string ActivityColor(ActivityKind kind, Theme theme, string providerId = "claude") => kind switch
    {
        ActivityKind.Attention => theme.Attention,
        ActivityKind.Running => theme.Running,
        ActivityKind.Done => providerId == "antigravity" && theme != Theme.Monochrome ? "#1A73E8" : theme.LogoDone,
        _ => theme.RingTrack,
    };

    /// <summary>Le snapshot vide du démarrage : aucune lecture, aucune note, jamais lu.</summary>
    public static bool IsWaitingForFirstReading(UsageSnapshot snapshot) =>
        snapshot.Windows.Count == 0 && snapshot.Note.Length == 0 && snapshot.FetchedAt == DateTimeOffset.MinValue;

    /// <summary>Assemble les cellules en modèle composite de pilule et calcule les dimensions et la couleur de bande repliée.</summary>
    public static PillModel Pill(
        IReadOnlyList<CellModel> cells,
        ScreenEdge edge,
        CellContent content,
        Theme theme,
        string providerMode = "claude")
    {
        var bodyLength = PillMetrics.BodyLengthFor(providerMode, edge, content);
        var windowLength = PillMetrics.WindowLengthFor(providerMode, edge, content);

        var hasAttention = cells.Any(c => c.Activity == ActivityKind.Attention);
        var bandColor = hasAttention
            ? theme.Attention
            : cells.Count > 0 ? cells[0].BandColor : theme.RingTrack;

        return new PillModel(cells, bodyLength, windowLength, bandColor, edge);
    }
}
