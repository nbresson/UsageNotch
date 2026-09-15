using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Tests.Settings;

public class SettingsStoreTests
{
    private static SettingsStore Build(TempDir dir) =>
        new(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);

    [Fact]
    public void Load_without_a_file_returns_defaults_and_writes_them()
    {
        using var dir = new TempDir();
        var store = Build(dir);

        var s = store.Load();

        s.Should().Be(new UsageNotch.Core.Settings.Settings());
        s.Port.Should().Be(48666);
        s.Edge.Should().Be(ScreenEdge.Right);
        s.Visibility.Should().Be(VisibilityMode.Expanded);
        s.Scale.Should().Be(1.0);
        File.ReadAllText(dir.File("settings.json")).Should().Contain("\"port\": 48666");
    }

    [Fact]
    public void Save_then_Load_round_trips_every_field()
    {
        using var dir = new TempDir();
        var store = Build(dir);
        var custom = Theme.Monochrome with { LevelCritical = "#FF0000", ThresholdWatch = 0.4 };
        var s = new UsageNotch.Core.Settings.Settings
        {
            Port = 50000,
            Edge = ScreenEdge.Top,
            MonitorDeviceId = @"\\.\DISPLAY2",
            Scale = 0.75,
            CellContent = CellContent.PercentOnly,
            Visibility = VisibilityMode.Folded,
            FoldedThicknessPx = 6,
            ThemePreset = ThemePreset.Custom,
            CustomTheme = custom,
            AutoOpenCard = false,
            SoundEnabled = false,
            DoneSound = "Hand",
            AttentionSound = "Question",
            TrayIconVisible = false,
            DebugLogging = true,
            AutoLaunch = false,
        }.WithPosition(ScreenEdge.Top, 0.25).WithPosition(ScreenEdge.Right, 0.9);

        store.Save(s);
        var loaded = Build(dir).Load();

        loaded.Should().Be(s);
        loaded.PositionFor(ScreenEdge.Top).Should().Be(0.25);
        loaded.PositionFor(ScreenEdge.Right).Should().Be(0.9);
        loaded.PositionFor(ScreenEdge.Left).Should().Be(0.5);
    }

    [Fact]
    public void Unknown_keys_are_ignored_and_missing_keys_get_defaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """{ "scale": 1.2, "futureOption": true, "edge": "Left" }""");

        var s = Build(dir).Load();

        s.Scale.Should().Be(1.2);
        s.Edge.Should().Be(ScreenEdge.Left);
        s.Port.Should().Be(48666);
        s.CustomTheme.Should().Be(Theme.Codenotch);
    }

    [Fact]
    public void Out_of_range_values_are_clamped_on_load()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"),
            """
            { "scale": 9, "foldedThicknessPx": 0, "port": 80, "positionRight": 4, "visibility": "Hidden", "trayIconVisible": false,
              "customTheme": { "pillOpacity": 0, "thresholdWatch": 0.9, "thresholdCritical": 0.3 } }
            """);

        var s = Build(dir).Load();

        s.Scale.Should().Be(1.5);
        s.FoldedThicknessPx.Should().Be(2);
        s.Port.Should().Be(48666);
        s.PositionFor(ScreenEdge.Right).Should().Be(1.0);
        s.TrayIconVisible.Should().BeTrue("le mode Masqué exige l'icône de notification");
        s.CustomTheme.PillOpacity.Should().Be(0.2);
        s.CustomTheme.ThresholdWatch.Should().Be(0.9);
        s.CustomTheme.ThresholdCritical.Should().Be(0.95, "critique est toujours au-dessus de surveillance");
        s.CustomTheme.PillBackground.Should().Be("#000000", "une couleur absente reprend le préréglage");
    }

    [Fact]
    public void A_corrupt_file_yields_defaults_without_throwing()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "{ corrupt");
        Build(dir).Load().Should().Be(new UsageNotch.Core.Settings.Settings());
    }

    [Fact]
    public void Save_is_atomic_and_publishes()
    {
        using var dir = new TempDir();
        var store = Build(dir);
        UsageNotch.Core.Settings.Settings? published = null;
        store.Changed += s => published = s;

        store.Save(new UsageNotch.Core.Settings.Settings { Scale = 0.5 });

        published!.Scale.Should().Be(0.5);
        store.Current.Scale.Should().Be(0.5);
        Directory.GetFiles(dir.Path).Should().ContainSingle().Which.Should().EndWith("settings.json");
    }

    [Fact]
    public void Null_values_in_a_hand_edited_file_fall_back_to_defaults()
    {
        using var dir = new TempDir();
        const string original = """{ "customTheme": null, "doneSound": null, "attentionSound": null, "edge": null, "scale": "big" }""";
        File.WriteAllText(dir.File("settings.json"), original);

        var s = Build(dir).Load();

        s.CustomTheme.Should().Be(Theme.Codenotch);
        s.DoneSound.Should().Be("Asterisk");
        s.AttentionSound.Should().Be("Exclamation");
        var copy = Directory.GetFiles(dir.Path, "settings.json.corrupt-*").Should().ContainSingle().Which;
        File.ReadAllText(copy).Should().Be(original);
    }

    [Fact]
    public void A_malformed_file_is_preserved_before_defaults_are_written()
    {
        using var dir = new TempDir();
        const string original = """{ "edge": "Diagonal", "port": 50000 }""";
        File.WriteAllText(dir.File("settings.json"), original);
        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        var s = new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance, time).Load();

        s.Should().Be(new UsageNotch.Core.Settings.Settings());
        File.ReadAllText(dir.File("settings.json.corrupt-1700000000")).Should().Be(original);
    }

    [Fact]
    public void An_existing_corrupt_copy_is_never_overwritten()
    {
        using var dir = new TempDir();
        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        File.WriteAllText(dir.File("settings.json.corrupt-1700000000"), "older");
        File.WriteAllText(dir.File("settings.json.corrupt-1700000000-1"), "older too");
        File.WriteAllText(dir.File("settings.json"), "{ corrupt");

        new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance, time).Load();

        File.ReadAllText(dir.File("settings.json.corrupt-1700000000")).Should().Be("older");
        File.ReadAllText(dir.File("settings.json.corrupt-1700000000-1")).Should().Be("older too");
        File.ReadAllText(dir.File("settings.json.corrupt-1700000000-2")).Should().Be("{ corrupt");
    }

    [Fact]
    public void A_valid_file_leaves_no_corrupt_copy()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """{ "scale": 0.8, "edge": "Left" }""");

        Build(dir).Load();

        Directory.GetFiles(dir.Path, "*corrupt*").Should().BeEmpty();
    }

    [Fact]
    public void A_number_written_as_a_string_is_read()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """{ "scale": "0.8", "port": "50000" }""");

        var s = Build(dir).Load();

        s.Scale.Should().Be(0.8);
        s.Port.Should().Be(50000);
    }

    [Fact]
    public void Auto_launch_defaults_to_true_and_is_written_camel_case()
    {
        using var dir = new TempDir();

        Build(dir).Load().AutoLaunch.Should().BeTrue();

        File.ReadAllText(dir.File("settings.json")).Should().Contain("\"autoLaunch\": true");
    }

    [Fact]
    public void A_null_custom_theme_alone_keeps_the_other_keys()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """{ "customTheme": null, "scale": 0.8 }""");

        var s = Build(dir).Load();

        s.CustomTheme.Should().Be(Theme.Codenotch);
        s.Scale.Should().Be(0.8);
    }

    [Fact]
    public void TryParse_accepts_a_valid_document_and_clamps_it()
    {
        var ok = SettingsStore.TryParse("""{ "scale": 9, "edge": "Left" }""", out var settings);

        ok.Should().BeTrue();
        settings.Scale.Should().Be(1.5);
        settings.Edge.Should().Be(ScreenEdge.Left);
    }

    [Fact]
    public void TryParse_rejects_malformed_json()
    {
        var ok = SettingsStore.TryParse("""{ "edge": """, out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryParse_rejects_a_wrong_enum_value()
    {
        var ok = SettingsStore.TryParse("""{ "edge": "Diagonal" }""", out _);

        ok.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.10, "#28E07B")]
    [InlineData(0.49, "#28E07B")]
    [InlineData(0.50, "#F5E400")]
    [InlineData(0.79, "#F5E400")]
    [InlineData(0.80, "#FF4500")]
    [InlineData(1.00, "#FF4500")]
    public void Theme_level_colour_follows_the_thresholds(double fraction, string expected) =>
        Theme.Codenotch.LevelColor(fraction).Should().Be(expected);

    [Fact]
    public void System_accent_preset_paints_ample_and_running_with_the_accent()
    {
        var theme = Theme.ForPreset(ThemePreset.SystemAccent, Theme.Codenotch, "#0078D4");
        theme.LevelAmple.Should().Be("#0078D4");
        theme.Running.Should().Be("#0078D4");
        theme.LevelCritical.Should().Be(Theme.Codenotch.LevelCritical);
        Theme.ForPreset(ThemePreset.SystemAccent, Theme.Codenotch, null).Should().Be(Theme.Codenotch);
    }

    [Fact]
    public void Hiding_in_fullscreen_is_on_by_default_and_read_from_the_file()
    {
        new UsageNotch.Core.Settings.Settings().HideInFullscreen.Should().BeTrue();

        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """{ "hideInFullscreen": false }""");
        Build(dir).Load().HideInFullscreen.Should().BeFalse();
    }

    [Fact]
    public void Threshold_notifications_default_on_at_eighty_percent()
    {
        var s = new UsageNotch.Core.Settings.Settings();

        s.ThresholdNotifications.Should().BeTrue();
        s.NotifyThreshold.Should().Be(0.8);
    }

    [Theory]
    [InlineData(0.1, 0.6)]
    [InlineData(0.75, 0.75)]
    [InlineData(1.2, 0.95)]
    public void Notify_threshold_is_clamped_between_sixty_and_ninety_five_percent(double written, double expected)
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), $$"""{ "notifyThreshold": {{written.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "thresholdNotifications": false }""");

        var s = Build(dir).Load();

        s.NotifyThreshold.Should().Be(expected);
        s.ThresholdNotifications.Should().BeFalse();
    }

    [Fact]
    public void Custom_preset_returns_the_custom_theme()
    {
        var custom = Theme.Monochrome with { Text = "#123456" };
        Theme.ForPreset(ThemePreset.Custom, custom, null).Should().Be(custom);
        Theme.ForPreset(ThemePreset.Monochrome, custom, null).Should().Be(Theme.Monochrome);
    }
}
