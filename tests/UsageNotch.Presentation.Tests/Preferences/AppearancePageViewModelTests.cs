using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class AppearancePageViewModelTests
{
    private static string Pct(int value) => value + FrenchText.Nbsp.ToString() + "%";

    private static (DraftFixture F, AppearancePageViewModel Vm, FakeColorPicker Picker, FakeAccent Accent) Create(
        Func<Settings, Settings>? initial = null)
    {
        var f = new DraftFixture(initial);
        var picker = new FakeColorPicker();
        var accent = new FakeAccent();
        return (f, new AppearancePageViewModel(f.Draft, accent, picker), picker, accent);
    }

    private static ColorSlot Slot(AppearancePageViewModel vm, string key) => vm.Colors.Single(c => c.Key == key);

    [Fact]
    public void Lists_presets_contents_and_the_fourteen_theme_colours_in_order()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Presets.Should().BeSameAs(Choices.ThemePresets);
        vm.CellContents.Should().BeSameAs(Choices.CellContents);
        vm.Colors.Select(c => c.Key).Should().Equal(
            "PillBackground", "PillBorder", "RingTrack", "LevelAmple", "LevelWatch", "LevelCritical",
            "RingSession", "RingWeeklyAll", "RingWeeklyScoped", "Running", "Attention", "Done", "LogoDone", "Text");
        vm.Colors.Select(c => c.Label).Should().Equal(
            "Fond de la pilule", "Contour de la pilule", "Piste de l'anneau", "Niveau modéré", "Niveau vigilance",
            "Niveau critique", "Anneau session", "Anneau hebdomadaire", "Anneau hebdo. par modèle",
            "Session en cours", "Session en attente", "Session terminée", "Logo, session terminée", "Texte");
        Slot(vm, "LevelAmple").Hex.Should().Be(Theme.Codenotch.LevelAmple);
    }

    [Fact]
    public void Editing_the_brand_colour_switches_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "LogoDone").Hex = "#123456";

        vm.Preset.Should().Be(ThemePreset.Custom);
        vm.Theme.LogoDone.Should().Be("#123456");
    }

    [Fact]
    public void Editing_a_ring_colour_switches_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "RingWeeklyScoped").Hex = "#123456";

        vm.Preset.Should().Be(ThemePreset.Custom);
        vm.Theme.RingWeeklyScoped.Should().Be("#123456");
    }

    [Fact]
    public void The_colouring_mode_is_offered_and_saved()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.RingColorings.Should().BeSameAs(Choices.RingColorings);

        vm.Coloring = RingColoring.ByLevel;

        vm.Coloring.Should().Be(RingColoring.ByLevel);
        f.Draft.Value.Coloring.Should().Be(RingColoring.ByLevel);
    }

    [Fact]
    public void Changing_the_colouring_mode_does_not_switch_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Coloring = RingColoring.ByLevel;

        vm.Preset.Should().Be(ThemePreset.Codenotch);
    }

    [Fact]
    public void Choosing_a_preset_edits_the_draft_and_refreshes_the_colours()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        var slotNames = new List<string?>();
        Slot(vm, "PillBackground").PropertyChanged += (_, e) => slotNames.Add(e.PropertyName);

        vm.Preset = ThemePreset.Monochrome;

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Monochrome);
        vm.Theme.Should().Be(Theme.Monochrome);
        Slot(vm, "PillBackground").Hex.Should().Be(Theme.Monochrome.PillBackground);
        names.Should().Contain(string.Empty);
        slotNames.Should().Contain(nameof(ColorSlot.Hex));
    }

    [Fact]
    public void Choosing_the_same_preset_again_changes_nothing()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Preset = ThemePreset.Codenotch;

        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Switching_to_custom_starts_from_the_theme_being_left()
    {
        var (f, vm, _, _) = Create(s => s with
        {
            ThemePreset = ThemePreset.Monochrome,
            CustomTheme = Theme.Codenotch with { Text = "#123456" },
        });
        using var _f = f;

        vm.Preset = ThemePreset.Custom;

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Custom);
        f.Draft.Value.CustomTheme.Should().Be(Theme.Monochrome);
    }

    [Fact]
    public void Editing_a_colour_on_a_preset_switches_to_custom_with_only_that_colour_changed()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "Text").Hex = "#abc";

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Custom);
        f.Draft.Value.CustomTheme.Should().Be(Theme.Codenotch with { Text = "#AABBCC" });
        Slot(vm, "Text").Hex.Should().Be("#AABBCC");
        Slot(vm, "Text").Error.Should().BeEmpty();
        vm.Preset.Should().Be(ThemePreset.Custom);
    }

    [Fact]
    public void Editing_a_custom_theme_keeps_its_other_colours()
    {
        var (f, vm, _, _) = Create(s => s with
        {
            ThemePreset = ThemePreset.Custom,
            CustomTheme = Theme.Codenotch with { Text = "#123456" },
        });
        using var _f = f;

        Slot(vm, "Done").Hex = "#00FF00";

        f.Draft.Value.CustomTheme.Should().Be(Theme.Codenotch with { Text = "#123456", Done = "#00FF00" });
    }

    [Fact]
    public void An_invalid_colour_shows_an_error_and_changes_nothing()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "Text").Hex = "rouge";

        Slot(vm, "Text").Error.Should().Be("Couleur invalide : « rouge ». Format attendu : #RRGGBB.");
        Slot(vm, "Text").Hex.Should().Be(Theme.Codenotch.Text);
        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void A_valid_entry_clears_the_previous_error()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        Slot(vm, "Text").Hex = "rouge";
        Slot(vm, "Text").Hex = "#FF0000";

        Slot(vm, "Text").Error.Should().BeEmpty();
        f.Draft.Value.CustomTheme.Text.Should().Be("#FF0000");
    }

    [Fact]
    public void Picking_a_colour_applies_it_and_cancelling_changes_nothing()
    {
        var (f, vm, picker, _) = Create();
        using var _f = f;

        picker.Result = null;
        Slot(vm, "LevelAmple").PickCommand.Execute(null);
        f.Draft.HasPendingEdits.Should().BeFalse();

        picker.Result = "#0078d4";
        Slot(vm, "LevelAmple").PickCommand.Execute(null);

        picker.Requests.Should().Equal(Theme.Codenotch.LevelAmple, Theme.Codenotch.LevelAmple);
        f.Draft.Value.CustomTheme.LevelAmple.Should().Be("#0078D4");
    }

    [Fact]
    public void Opacity_is_a_theme_setting_and_is_clamped()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PillOpacity = 0.1;

        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Custom);
        f.Draft.Value.CustomTheme.PillOpacity.Should().Be(0.2);
        vm.PillOpacity.Should().Be(0.2);
        vm.PillOpacityText.Should().Be(Pct(20));
    }

    [Fact]
    public void Raising_the_watch_threshold_pushes_the_critical_threshold_up()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.ThresholdWatch = 0.9;

        vm.ThresholdWatch.Should().Be(0.9);
        vm.ThresholdCritical.Should().BeApproximately(0.95, 1e-9);
        vm.ThresholdWatchText.Should().Be(Pct(90));
        vm.ThresholdCriticalText.Should().Be(Pct(95));
    }

    [Fact]
    public void Scale_and_cell_content_edit_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Scale = 1.25;
        vm.CellContent = CellContent.RingOnly;

        f.Draft.Value.Scale.Should().Be(1.25);
        f.Draft.Value.CellContent.Should().Be(CellContent.RingOnly);
        vm.ScaleText.Should().Be(Pct(125));
        f.Draft.Value.ThemePreset.Should().Be(ThemePreset.Codenotch);
    }

    [Fact]
    public void The_system_accent_preset_shows_the_windows_accent()
    {
        var (f, vm, _, accent) = Create();
        using var _f = f;
        accent.AccentHex = "#0078D4";

        vm.Preset = ThemePreset.SystemAccent;

        vm.Theme.LevelAmple.Should().Be("#0078D4");
        Slot(vm, "LevelAmple").Hex.Should().Be("#0078D4");
    }

    [Fact]
    public void Dispose_stops_following_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;
        var count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.Dispose();
        f.Draft.Edit(s => s with { Scale = 1.3 });

        count.Should().Be(0);
    }

    [Fact]
    public void The_provider_is_offered_and_saved()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Providers.Should().BeSameAs(Choices.Providers);
        vm.ProviderChoices.Should().BeSameAs(Choices.Providers);
        vm.Provider.Should().Be("both");

        vm.Provider = "antigravity";

        vm.Provider.Should().Be("antigravity");
        f.Draft.Value.Provider.Should().Be("antigravity");
    }

    [Fact]
    public void Changing_the_provider_does_not_switch_to_the_custom_theme()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Provider = "antigravity";

        vm.Preset.Should().Be(ThemePreset.Codenotch);
    }

    [Fact]
    public void OpenAi_settings_and_activation_flag_are_reflected_and_saved()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        // Default provider is "both" (Claude + Antigravity), so OpenAI is not active
        vm.IsOpenAiActive.Should().BeFalse();

        vm.Provider = "all";
        vm.IsOpenAiActive.Should().BeTrue();

        vm.Provider = "claude_openai";
        vm.IsOpenAiActive.Should().BeTrue();

        vm.Provider = "antigravity_openai";
        vm.IsOpenAiActive.Should().BeTrue();

        vm.Provider = "openai";
        vm.IsOpenAiActive.Should().BeTrue();

        vm.Provider = "claude";
        vm.IsOpenAiActive.Should().BeFalse();

        vm.OpenAiApiKey = "sk-custom-test-key";
        vm.OpenAiApiKey.Should().Be("sk-custom-test-key");
        f.Draft.Value.OpenAiApiKey.Should().Be("sk-custom-test-key");

        vm.OpenAiMonthlyBudget = 75.0;
        vm.OpenAiMonthlyBudget.Should().Be(75.0);
        vm.OpenAiMonthlyBudgetText.Should().Be("75 $");
        f.Draft.Value.OpenAiMonthlyBudget.Should().Be(75.0);

        // OpenAI mode and session token
        vm.OpenAiModes.Should().BeSameAs(Choices.OpenAiModes);
        vm.OpenAiMode.Should().Be("subscription");
        vm.IsOpenAiSubscriptionMode.Should().BeTrue();
        vm.IsOpenAiApiMode.Should().BeFalse();

        vm.OpenAiSessionToken = "sess-12345";
        vm.OpenAiSessionToken.Should().Be("sess-12345");
        f.Draft.Value.OpenAiSessionToken.Should().Be("sess-12345");

        vm.OpenAiMode = "api";
        vm.OpenAiMode.Should().Be("api");
        vm.IsOpenAiSubscriptionMode.Should().BeFalse();
        vm.IsOpenAiApiMode.Should().BeTrue();
        f.Draft.Value.OpenAiMode.Should().Be("api");
    }
}
