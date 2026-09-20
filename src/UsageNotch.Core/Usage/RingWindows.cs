namespace UsageNotch.Core.Usage;

/// <summary>
/// Les fenêtres que la pilule dessine, de l'anneau extérieur à l'intérieur. Chaque groupe liste les <c>kind</c>
/// acceptés dans l'ordre d'essai : l'API n'emploie pas le même d'un compte et d'une version à l'autre.
/// </summary>
public static class RingWindows
{
    public static readonly IReadOnlyList<string> Session = ["session", "five_hour"];
    public static readonly IReadOnlyList<string> WeeklyAll = ["weekly_all", "seven_day", "weekly"];
    public static readonly IReadOnlyList<string> WeeklyScoped = ["weekly_scoped", "weekly_opus", "seven_day_opus"];

    public static readonly IReadOnlyList<IReadOnlyList<string>> Claude = [Session, WeeklyAll, WeeklyScoped];
}
