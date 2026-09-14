namespace UsageNotch.Core.Settings;

/// <summary>Couleurs en <c>#RRGGBB</c> (Core ne dépend pas de WPF ; l'App les convertit), opacité du fond et seuils de niveau.</summary>
public sealed record Theme(
    string PillBackground,
    string PillBorder,
    string RingTrack,
    string LevelAmple,
    string LevelWatch,
    string LevelCritical,
    string Running,
    string Attention,
    string Done,
    string Text,
    double PillOpacity,
    double ThresholdWatch,
    double ThresholdCritical)
{
    public static Theme Codenotch { get; } = new(
        PillBackground: "#000000", PillBorder: "#2E2E2E", RingTrack: "#3A3A3A",
        LevelAmple: "#28E07B", LevelWatch: "#F5E400", LevelCritical: "#FF4500",
        Running: "#28E07B", Attention: "#FFBF00", Done: "#57C7FF",
        Text: "#FFFFFF", PillOpacity: 1.0, ThresholdWatch: 0.50, ThresholdCritical: 0.80);

    public static Theme Monochrome { get; } = new(
        PillBackground: "#111111", PillBorder: "#3A3A3A", RingTrack: "#333333",
        LevelAmple: "#E0E0E0", LevelWatch: "#A0A0A0", LevelCritical: "#FFFFFF",
        Running: "#E0E0E0", Attention: "#FFFFFF", Done: "#B0B0B0",
        Text: "#FFFFFF", PillOpacity: 0.9, ThresholdWatch: 0.50, ThresholdCritical: 0.80);

    /// <summary>Le thème effectif. L'accent système est fourni par l'App ; sans accent, le préréglage Codenotch sert de repli.</summary>
    public static Theme ForPreset(ThemePreset preset, Theme custom, string? systemAccentHex) => preset switch
    {
        ThemePreset.Monochrome => Monochrome,
        ThemePreset.SystemAccent => systemAccentHex is null
            ? Codenotch
            : Codenotch with { LevelAmple = systemAccentHex, Running = systemAccentHex },
        ThemePreset.Custom => custom,
        _ => Codenotch,
    };

    public string LevelColor(double fraction) =>
        fraction >= ThresholdCritical ? LevelCritical
        : fraction >= ThresholdWatch ? LevelWatch
        : LevelAmple;

    /// <summary>Bornes des seuils et de l'opacité ; une couleur absente d'un JSON partiel reprend celle du préréglage Codenotch.</summary>
    public Theme Clamp()
    {
        var watch = Math.Clamp(ThresholdWatch, 0.05, 0.95);
        // Arrondi : 0.9 + 0.05 vaut 0.9500000000000001 en double.
        var critical = Math.Clamp(ThresholdCritical, Math.Round(watch + 0.05, 6), 1.0);
        var d = Codenotch;
        return this with
        {
            PillBackground = Or(PillBackground, d.PillBackground),
            PillBorder = Or(PillBorder, d.PillBorder),
            RingTrack = Or(RingTrack, d.RingTrack),
            LevelAmple = Or(LevelAmple, d.LevelAmple),
            LevelWatch = Or(LevelWatch, d.LevelWatch),
            LevelCritical = Or(LevelCritical, d.LevelCritical),
            Running = Or(Running, d.Running),
            Attention = Or(Attention, d.Attention),
            Done = Or(Done, d.Done),
            Text = Or(Text, d.Text),
            PillOpacity = Math.Clamp(PillOpacity, 0.2, 1.0),
            ThresholdWatch = watch,
            ThresholdCritical = critical,
        };
    }

    // Un record positionnel désérialisé depuis un JSON partiel peut porter null malgré le type non-nullable.
    private static string Or(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
}
