using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Pill;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

public sealed record PreviewSample(string Caption, CellModel Cell, PillModel? Pill = null);

public sealed record PreviewModel(
    Theme Theme,
    ScreenEdge Edge,
    double Scale,
    VisibilityMode Visibility,
    int FoldedThicknessPx,
    IReadOnlyList<PreviewSample> Samples,
    string Provider = "both",
    CellContent CellContent = CellContent.RingAndPercent);

/// <summary>Trois pilules d'exemple, une par niveau d'usage, calculées exactement comme la vraie pilule.</summary>
public static class SettingsPreview
{
    public static readonly DateTimeOffset SampleTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static PreviewModel Build(CoreSettings settings, string? accentHex)
    {
        var theme = Theme.ForPreset(settings.ThemePreset, settings.CustomTheme, accentHex);
        var content = settings.CellContent;
        var provider = settings.Provider;
        var edge = settings.Edge;
        PreviewSample[] samples =
        [
            Sample("Modéré · en cours", theme.ThresholdWatch / 2, SessionState.Running, theme, content, settings.Coloring, provider, edge),
            Sample("Vigilance · en attente", (theme.ThresholdWatch + theme.ThresholdCritical) / 2, SessionState.Attention, theme, content, settings.Coloring, provider, edge),
            Sample("Critique · terminé", (theme.ThresholdCritical + 1.0) / 2, SessionState.Done, theme, content, settings.Coloring, provider, edge),
        ];
        return new PreviewModel(theme, settings.Edge, settings.Scale, settings.Visibility, settings.FoldedThicknessPx, samples, provider, content);
    }

    private static PreviewSample Sample(
        string caption, double fraction, SessionState state, Theme theme, CellContent content, RingColoring coloring, string provider, ScreenEdge edge)
    {
        if (provider == "both")
        {
            var claudeSnap = CreateSnapshot(fraction, isAntigravity: false);
            var agySnap = CreateSnapshot(fraction * 0.9, isAntigravity: true);

            var claudeCell = PillPresenter.Cell(claudeSnap, RingWindows.Claude, state, theme, content, coloring, SampleTime, "claude");
            var agyCell = PillPresenter.Cell(agySnap, RingWindows.Antigravity, SessionState.Idle, theme, content, coloring, SampleTime, "antigravity");

            var pill = PillPresenter.Pill([claudeCell, agyCell], edge, content, theme, "both");
            return new PreviewSample(caption, claudeCell, pill);
        }

        var isAg = provider == "antigravity";
        var singleSnap = CreateSnapshot(fraction, isAg);
        var rings = isAg ? RingWindows.Antigravity : RingWindows.Claude;
        var cell = PillPresenter.Cell(singleSnap, rings, state, theme, content, coloring, SampleTime, provider);
        var singlePill = PillPresenter.Pill([cell], edge, content, theme, provider);
        return new PreviewSample(caption, cell, singlePill);
    }

    private static UsageSnapshot CreateSnapshot(double fraction, bool isAntigravity) =>
        isAntigravity
            ? new UsageSnapshot(
                SnapshotStatus.Ok,
                [
                    new LimitWindow("gemini-5h", "Modèles Gemini (5 h)", fraction, SampleTime.AddHours(3)),
                    new LimitWindow("gemini-weekly", "Modèles Gemini (hebdomadaire)", fraction * 0.6, SampleTime.AddDays(3)),
                    new LimitWindow("3p-weekly", "Modèles tiers (hebdomadaire)", fraction * 0.3, SampleTime.AddDays(3)),
                ],
                SampleTime,
                "",
                null)
            : new UsageSnapshot(
                SnapshotStatus.Ok,
                [
                    new LimitWindow("session", "Session en cours", fraction, SampleTime.AddHours(3)),
                    new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", fraction * 0.6, SampleTime.AddDays(3)),
                    new LimitWindow("weekly_scoped", "Hebdomadaire (par modèle)", fraction * 0.3, SampleTime.AddDays(3)),
                ],
                SampleTime,
                "",
                null);
}
