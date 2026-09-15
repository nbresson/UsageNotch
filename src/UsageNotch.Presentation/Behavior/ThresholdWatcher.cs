using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Behavior;

public enum ThresholdLevel
{
    Warning,
    Exhausted,
}

public sealed record ThresholdAlert(string WindowId, ThresholdLevel Level, string Title, string Message);

/// <summary>
/// Détecte le franchissement du seuil d'alerte puis de 100 % pour chaque fenêtre de limite d'une lecture fraîche.
/// Chaque niveau n'est annoncé qu'une fois par période (jusqu'à la réinitialisation de la fenêtre), y compris après un
/// redémarrage grâce à <see cref="ThresholdLog"/>. Atteindre la limite d'un coup n'annonce que la limite.
/// Non thread-safe : appeler depuis le thread UI.
/// </summary>
public sealed class ThresholdWatcher(ThresholdLog log)
{
    /// <summary>Une réinitialisation est identifiée au quart d'heure près : l'heure renvoyée peut varier de quelques secondes.</summary>
    private const long PeriodResolutionSeconds = 15 * 60;

    public IReadOnlyList<ThresholdAlert> Observe(UsageSnapshot snapshot, double threshold, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (snapshot.Status != SnapshotStatus.Ok) return [];

        var alerts = new List<ThresholdAlert>();
        foreach (var window in snapshot.Windows)
        {
            if (window.ResetsAt <= now) continue;

            ThresholdLevel? level = window.UsedFraction >= 1.0 ? ThresholdLevel.Exhausted
                : window.UsedFraction >= threshold ? ThresholdLevel.Warning
                : null;
            if (level is not { } reached) continue;

            var key = Key(window, reached);
            if (log.Contains(key)) continue;

            log.Add(key, window.ResetsAt);
            if (reached == ThresholdLevel.Exhausted) log.Add(Key(window, ThresholdLevel.Warning), window.ResetsAt);
            alerts.Add(new ThresholdAlert(window.Id, reached, Title(window, reached), FrenchText.ResetCopy(window.ResetsAt, now, zone)));
        }
        return alerts;
    }

    private static string Key(LimitWindow window, ThresholdLevel level) =>
        $"{window.Id}|{level}|{window.ResetsAt.ToUnixTimeSeconds() / PeriodResolutionSeconds}";

    private static string Title(LimitWindow window, ThresholdLevel level) => level == ThresholdLevel.Exhausted
        ? $"{window.Label} : limite atteinte"
        : $"{window.Label} : {FrenchText.Percent(window.UsedFraction)} utilisés";
}
