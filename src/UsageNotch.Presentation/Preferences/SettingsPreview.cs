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
        IReadOnlyList<string> activeProviderIds = provider switch
        {
            "all" => ["claude", "antigravity", "openai"],
            "both" => ["claude", "antigravity"],
            "claude_openai" => ["claude", "openai"],
            "antigravity_openai" => ["antigravity", "openai"],
            "antigravity" => ["antigravity"],
            "openai" => ["openai"],
            _ => ["claude"]
        };

        var cells = new List<CellModel>();
        for (int i = 0; i < activeProviderIds.Count; i++)
        {
            var pid = activeProviderIds[i];
            var f = fraction * (1.0 - i * 0.1);
            var snap = CreateSnapshot(f, pid);
            var rings = pid switch
            {
                "antigravity" => RingWindows.Antigravity,
                "openai" => RingWindows.OpenAi,
                _ => RingWindows.Claude
            };
            var s = i == 0 ? state : SessionState.Idle;
            cells.Add(PillPresenter.Cell(snap, rings, s, theme, content, coloring, SampleTime, pid));
        }

        var pill = PillPresenter.Pill(cells, edge, content, theme, provider);
        return new PreviewSample(caption, cells[0], pill);
    }

    private static UsageSnapshot CreateSnapshot(double fraction, string providerId) =>
        providerId switch
        {
            "antigravity" => new UsageSnapshot(
                SnapshotStatus.Ok,
                [
                    new LimitWindow("gemini-5h", "Modèles Gemini (5 h)", fraction, SampleTime.AddHours(3)),
                    new LimitWindow("gemini-weekly", "Modèles Gemini (hebdomadaire)", fraction * 0.6, SampleTime.AddDays(3)),
                    new LimitWindow("3p-weekly", "Modèles tiers (hebdomadaire)", fraction * 0.3, SampleTime.AddDays(3)),
                ],
                SampleTime,
                "",
                null),
            "openai" => new UsageSnapshot(
                SnapshotStatus.Ok,
                [
                    new LimitWindow("monthly_cost", "Budget mensuel ($12.00 / $20)", fraction, SampleTime.AddDays(15)),
                    new LimitWindow("daily_cost", "Consommation du jour ($1.50)", fraction * 0.6, SampleTime.AddHours(6)),
                    new LimitWindow("reasoning_models", "Modèles raisonnement o1/o3 ($4.00)", fraction * 0.3, SampleTime.AddDays(15)),
                ],
                SampleTime,
                "",
                null),
            _ => new UsageSnapshot(
                SnapshotStatus.Ok,
                [
                    new LimitWindow("session", "Session en cours", fraction, SampleTime.AddHours(3)),
                    new LimitWindow("weekly_all", "Hebdomadaire (tous modèles)", fraction * 0.6, SampleTime.AddDays(3)),
                    new LimitWindow("weekly_scoped", "Hebdomadaire (par modèle)", fraction * 0.3, SampleTime.AddDays(3)),
                ],
                SampleTime,
                "",
                null)
        };
}
