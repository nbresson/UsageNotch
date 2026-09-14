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
        string headlineWindowId,
        SessionState aggregate,
        Theme theme,
        CellContent content,
        DateTimeOffset now)
    {
        var activity = ActivityOf(aggregate);
        var headline = snapshot.Status == SnapshotStatus.NeedsAuth ? null : snapshot.Window(headlineWindowId);

        double? fraction = headline is null ? null : Math.Clamp(headline.UsedFraction, 0.0, 1.0);
        var ringColor = fraction is { } f ? theme.LevelColor(f) : theme.RingTrack;
        var percent = fraction is { } p
            ? FrenchText.Percent(p)
            : IsWaitingForFirstReading(snapshot) ? "…" : "—";

        var dimmed = snapshot.Status == SnapshotStatus.Stale
            || (snapshot.FetchedAt != DateTimeOffset.MinValue && now - snapshot.FetchedAt > StaleAfter);

        return new CellModel(
            RingFraction: fraction,
            RingColor: ringColor,
            TrackColor: theme.RingTrack,
            PercentText: percent,
            TextColor: theme.Text,
            ShowRing: content != CellContent.PercentOnly,
            ShowPercent: content != CellContent.RingOnly,
            Dimmed: dimmed,
            Exhausted: fraction >= 1.0,
            Activity: activity,
            ActivityColor: ActivityColor(activity, theme),
            BandColor: activity == ActivityKind.Attention ? theme.Attention : ringColor);
    }

    public static ActivityKind ActivityOf(SessionState state) => state switch
    {
        SessionState.Attention => ActivityKind.Attention,
        SessionState.Running => ActivityKind.Running,
        SessionState.Done => ActivityKind.Done,
        _ => ActivityKind.None,
    };

    public static string ActivityColor(ActivityKind kind, Theme theme) => kind switch
    {
        ActivityKind.Attention => theme.Attention,
        ActivityKind.Running => theme.Running,
        ActivityKind.Done => theme.Done,
        _ => theme.RingTrack,
    };

    /// <summary>Le snapshot vide du démarrage : aucune lecture, aucune note, jamais lu.</summary>
    public static bool IsWaitingForFirstReading(UsageSnapshot snapshot) =>
        snapshot.Windows.Count == 0 && snapshot.Note.Length == 0 && snapshot.FetchedAt == DateTimeOffset.MinValue;
}
