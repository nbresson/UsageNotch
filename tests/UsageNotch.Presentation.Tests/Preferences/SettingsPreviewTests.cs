using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Pill;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class SettingsPreviewTests
{
    private static string Pct(int value) => value + FrenchText.Nbsp.ToString() + "%";

    [Fact]
    public void Three_samples_show_each_usage_level_and_session_state()
    {
        var model = SettingsPreview.Build(new Settings(), accentHex: null);
        var theme = Theme.Codenotch;

        model.Theme.Should().Be(theme);
        model.Samples.Select(s => s.Caption).Should().Equal("Modéré · en cours", "Vigilance · en attente", "Critique · terminé");
        model.Samples.Select(s => s.Cell.PercentText).Should().Equal(Pct(25), Pct(65), Pct(90));
        model.Samples.Select(s => s.Cell.RingColor).Should().Equal(theme.LevelAmple, theme.LevelWatch, theme.LevelCritical);
        model.Samples.Select(s => s.Cell.Activity).Should().Equal(ActivityKind.Running, ActivityKind.Attention, ActivityKind.Done);
        model.Samples.Should().OnlyContain(s => !s.Cell.Dimmed);
    }

    [Fact]
    public void Samples_follow_custom_thresholds()
    {
        var settings = new Settings
        {
            ThemePreset = ThemePreset.Custom,
            CustomTheme = Theme.Codenotch with { ThresholdWatch = 0.3, ThresholdCritical = 0.6 },
        };

        var model = SettingsPreview.Build(settings, accentHex: null);

        model.Samples[0].Cell.RingFraction.Should().BeApproximately(0.15, 1e-9);
        model.Samples[1].Cell.RingFraction.Should().BeApproximately(0.45, 1e-9);
        model.Samples[2].Cell.RingFraction.Should().BeApproximately(0.8, 1e-9);
        model.Samples.Select(s => s.Cell.RingColor).Should().Equal(Theme.Codenotch.LevelAmple, Theme.Codenotch.LevelWatch, Theme.Codenotch.LevelCritical);
    }

    [Fact]
    public void The_theme_follows_the_preset_and_the_system_accent()
    {
        var model = SettingsPreview.Build(new Settings { ThemePreset = ThemePreset.SystemAccent }, "#0078D4");

        model.Theme.LevelAmple.Should().Be("#0078D4");
        model.Samples[0].Cell.RingColor.Should().Be("#0078D4");
    }

    [Fact]
    public void Cell_content_is_honoured()
    {
        var model = SettingsPreview.Build(new Settings { CellContent = CellContent.PercentOnly }, accentHex: null);

        model.Samples.Should().OnlyContain(s => !s.Cell.ShowRing && s.Cell.ShowPercent);
    }

    [Fact]
    public void Placement_and_visibility_settings_are_passed_through()
    {
        var settings = new Settings { Edge = ScreenEdge.Top, Scale = 0.8, Visibility = VisibilityMode.Folded, FoldedThicknessPx = 7 };

        var model = SettingsPreview.Build(settings, accentHex: null);

        model.Edge.Should().Be(ScreenEdge.Top);
        model.Scale.Should().Be(0.8);
        model.Visibility.Should().Be(VisibilityMode.Folded);
        model.FoldedThicknessPx.Should().Be(7);
    }
}
