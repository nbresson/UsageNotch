using UsageNotch.Core.Settings;

namespace UsageNotch.Presentation.Preferences;

/// <summary>Une valeur et son libellé français. <see cref="ToString"/> rend le libellé, lu par les listes et l'accessibilité.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Les listes de choix de la fenêtre de réglages, dans l'ordre d'affichage.</summary>
public static class Choices
{
    public static IReadOnlyList<Choice<ThemePreset>> ThemePresets { get; } =
    [
        new(ThemePreset.Codenotch, "Codenotch"),
        new(ThemePreset.Monochrome, "Monochrome"),
        new(ThemePreset.SystemAccent, "Accent système"),
        new(ThemePreset.Custom, "Personnalisé"),
    ];

    public static IReadOnlyList<Choice<CellContent>> CellContents { get; } =
    [
        new(CellContent.RingAndPercent, "Anneau et pourcentage"),
        new(CellContent.RingOnly, "Anneau seul"),
        new(CellContent.PercentOnly, "Pourcentage seul"),
    ];

    public static IReadOnlyList<Choice<ScreenEdge>> Edges { get; } =
    [
        new(ScreenEdge.Right, "Droite"),
        new(ScreenEdge.Left, "Gauche"),
        new(ScreenEdge.Top, "Haut"),
        new(ScreenEdge.Bottom, "Bas"),
    ];

    public static IReadOnlyList<Choice<VisibilityMode>> Visibilities { get; } =
    [
        new(VisibilityMode.Expanded, "Déplié"),
        new(VisibilityMode.Folded, "Replié"),
        new(VisibilityMode.Hidden, "Masqué"),
    ];

    /// <summary>Noms acceptés par <c>ISoundPlayer.Play</c>.</summary>
    public static IReadOnlyList<Choice<string>> Sounds { get; } =
    [
        new("Asterisk", "Astérisque"),
        new("Beep", "Bip"),
        new("Exclamation", "Exclamation"),
        new("Hand", "Arrêt critique"),
        new("Question", "Question"),
    ];

    public static string SoundLabel(string name) => Sounds.FirstOrDefault(s => s.Value == name)?.Label ?? name;
}
