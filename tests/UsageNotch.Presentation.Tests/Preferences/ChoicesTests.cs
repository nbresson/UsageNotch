using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class ChoicesTests
{
    [Fact]
    public void A_choice_reads_as_its_label()
    {
        new Choice<ScreenEdge>(ScreenEdge.Top, "Haut").ToString().Should().Be("Haut");
    }

    [Fact]
    public void Enumerations_are_listed_in_spec_order_with_french_labels()
    {
        Choices.ThemePresets.Select(c => c.Value).Should().Equal(ThemePreset.Codenotch, ThemePreset.Monochrome, ThemePreset.SystemAccent, ThemePreset.Custom);
        Choices.ThemePresets.Select(c => c.Label).Should().Equal("Codenotch", "Monochrome", "Accent système", "Personnalisé");
        Choices.CellContents.Select(c => c.Label).Should().Equal("Anneau et pourcentage", "Anneau seul", "Pourcentage seul");
        Choices.Edges.Select(c => c.Value).Should().Equal(ScreenEdge.Right, ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Bottom);
        Choices.Edges.Select(c => c.Label).Should().Equal("Droite", "Gauche", "Haut", "Bas");
        Choices.Visibilities.Select(c => c.Label).Should().Equal("Déplié", "Replié", "Masqué");
    }

    [Fact]
    public void Sounds_are_the_five_windows_system_sounds()
    {
        Choices.Sounds.Select(c => c.Value).Should().Equal("Asterisk", "Beep", "Exclamation", "Hand", "Question");
        Choices.Sounds.Select(c => c.Label).Should().Equal("Astérisque", "Bip", "Exclamation", "Arrêt critique", "Question");
    }

    [Fact]
    public void An_unknown_sound_keeps_its_name_as_label()
    {
        Choices.SoundLabel("Hand").Should().Be("Arrêt critique");
        Choices.SoundLabel("Tada").Should().Be("Tada");
    }
}
