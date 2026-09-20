using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Card;

public static class CardPresenter
{
    public const int MaxSessions = 5;

    /// <summary>Les sessions arrivent déjà triées par SessionStore.Snapshot() (état puis début décroissant).</summary>
    public static CardModel Build(
        UsageSnapshot snapshot,
        string displayName,
        IReadOnlyList<Session> sessions,
        Theme theme,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var waiting = PillPresenter.IsWaitingForFirstReading(snapshot);

        var neverRead = snapshot.FetchedAt == DateTimeOffset.MinValue;
        var dimmed = !neverRead && (snapshot.Status == SnapshotStatus.Stale || now - snapshot.FetchedAt > PillPresenter.StaleAfter);
        var subtitle = dimmed ? FrenchText.UpdatedAgo(snapshot.FetchedAt, now) : null;

        IReadOnlyList<WindowRow> windows = snapshot.Status == SnapshotStatus.NeedsAuth
            ? []
            : snapshot.Windows.Select(w => Row(w, theme, now, zone)).ToList();

        var note = snapshot.Note.Length > 0
            ? snapshot.Note
            : waiting ? "En attente de la première lecture…" : null;

        var rows = sessions
            .Where(s => s.State != SessionState.Idle)
            .Take(MaxSessions)
            .Select(s => SessionRowOf(s, theme))
            .ToList();

        return new CardModel(displayName, subtitle, windows, note, rows);
    }

    private static WindowRow Row(LimitWindow w, Theme theme, DateTimeOffset now, TimeZoneInfo zone)
    {
        var fraction = Math.Clamp(w.UsedFraction, 0.0, 1.0);
        return new WindowRow(
            w.Label,
            FrenchText.ResetCopy(w.ResetsAt, now, zone),
            fraction,
            theme.LevelColor(fraction),
            FrenchText.Percent(fraction) + " utilisé");
    }

    private static SessionRow SessionRowOf(Session s, Theme theme)
    {
        var activity = PillPresenter.ActivityOf(s.State);
        var detail = s.State switch
        {
            SessionState.Attention => s.AttentionMessage.Length > 0 ? s.AttentionMessage : "Attend votre réponse",
            SessionState.Running => s.LastAction.Length > 0 ? s.LastAction : s.Prompt.Length > 0 ? s.Prompt : "En cours",
            SessionState.Done => "Terminé en " + FrenchText.Duration(s.Total),
            _ => "",
        };
        return new SessionRow(s.Id, s.Title, detail, activity, RowColor(activity, theme));
    }

    /// <summary>
    /// La carte garde ses propres couleurs d'état. La pilule fait porter « terminé » par la couleur de marque
    /// du logo ; la carte reste sur <see cref="Theme.Done"/>, que son réglage « Session terminée » désigne.
    /// Duplication assumée : deux langages visuels que rien n'oblige à rester liés.
    /// </summary>
    private static string RowColor(ActivityKind kind, Theme theme) => kind switch
    {
        ActivityKind.Attention => theme.Attention,
        ActivityKind.Running => theme.Running,
        ActivityKind.Done => theme.Done,
        _ => theme.RingTrack,
    };
}
