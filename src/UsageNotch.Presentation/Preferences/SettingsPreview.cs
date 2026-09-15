using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Pill;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Presentation.Preferences;

public sealed record PreviewSample(string Caption, CellModel Cell);

public sealed record PreviewModel(
    Theme Theme,
    ScreenEdge Edge,
    double Scale,
    VisibilityMode Visibility,
    int FoldedThicknessPx,
    IReadOnlyList<PreviewSample> Samples);

/// <summary>Trois pilules d'exemple, une par niveau d'usage, calculées exactement comme la vraie pilule.</summary>
public static class SettingsPreview
{
    public static readonly DateTimeOffset SampleTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static PreviewModel Build(CoreSettings settings, string? accentHex)
    {
        var theme = Theme.ForPreset(settings.ThemePreset, settings.CustomTheme, accentHex);
        var content = settings.CellContent;
        PreviewSample[] samples =
        [
            Sample("Modéré · en cours", theme.ThresholdWatch / 2, SessionState.Running, theme, content),
            Sample("Vigilance · en attente", (theme.ThresholdWatch + theme.ThresholdCritical) / 2, SessionState.Attention, theme, content),
            Sample("Critique · terminé", (theme.ThresholdCritical + 1.0) / 2, SessionState.Done, theme, content),
        ];
        return new PreviewModel(theme, settings.Edge, settings.Scale, settings.Visibility, settings.FoldedThicknessPx, samples);
    }

    private static PreviewSample Sample(string caption, double fraction, SessionState state, Theme theme, CellContent content)
    {
        var snapshot = new UsageSnapshot(
            SnapshotStatus.Ok,
            [new LimitWindow("session", "Session en cours", fraction, SampleTime.AddHours(3))],
            SampleTime,
            "",
            null);
        return new PreviewSample(caption, PillPresenter.Cell(snapshot, "session", state, theme, content, SampleTime));
    }
}
