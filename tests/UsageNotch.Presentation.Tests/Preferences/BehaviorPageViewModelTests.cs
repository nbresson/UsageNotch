using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class BehaviorPageViewModelTests
{
    private static (DraftFixture F, BehaviorPageViewModel Vm, FakeSound Sound, FakeAutoStart AutoStart) Create(
        FakeAutoStart? autoStart = null)
    {
        var f = new DraftFixture();
        var sound = new FakeSound();
        autoStart ??= new FakeAutoStart();
        return (f, new BehaviorPageViewModel(f.Draft, sound, autoStart), sound, autoStart);
    }

    [Fact]
    public void Card_and_sound_switches_edit_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.AutoOpenCard = false;
        vm.SoundEnabled = false;

        f.Draft.Value.AutoOpenCard.Should().BeFalse();
        f.Draft.Value.SoundEnabled.Should().BeFalse();
    }

    [Fact]
    public void Sound_choices_are_the_windows_sounds_and_a_selection_edits_the_draft()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.Sounds.Should().BeSameAs(Choices.Sounds);
        vm.DoneSound.Should().Be("Asterisk");

        vm.DoneSound = "Hand";
        vm.AttentionSound = "Question";

        f.Draft.Value.DoneSound.Should().Be("Hand");
        f.Draft.Value.AttentionSound.Should().Be("Question");
    }

    [Fact]
    public void An_empty_sound_selection_is_ignored()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.DoneSound = null!;
        vm.AttentionSound = "";

        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Preview_buttons_play_the_selected_sound_even_when_sounds_are_off()
    {
        var (f, vm, sound, _) = Create();
        using var _f = f;

        vm.SoundEnabled = false;
        vm.DoneSound = "Question";
        vm.PlayDoneSoundCommand.Execute(null);
        vm.PlayAttentionSoundCommand.Execute(null);

        sound.Played.Should().Equal("Question", "Exclamation");
    }

    [Fact]
    public void Auto_start_reflects_and_changes_the_registry_value()
    {
        var (f, vm, _, autoStart) = Create();
        using var _f = f;

        vm.AutoStartAvailable.Should().BeTrue();
        vm.AutoStartEnabled.Should().BeFalse();
        vm.AutoStartNote.Should().BeEmpty();

        vm.AutoStartEnabled = true;

        autoStart.Writes.Should().Equal(true);
        vm.AutoStartEnabled.Should().BeTrue();
        vm.AutoStartError.Should().BeEmpty();
        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void A_failed_auto_start_change_keeps_the_previous_state_and_shows_the_error()
    {
        var (f, vm, _, _) = Create(new FakeAutoStart { Error = "Impossible de modifier le démarrage avec Windows : accès refusé" });
        using var _f = f;
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        vm.AutoStartEnabled = true;

        vm.AutoStartEnabled.Should().BeFalse();
        vm.AutoStartError.Should().Be("Impossible de modifier le démarrage avec Windows : accès refusé");
        names.Should().Contain(nameof(BehaviorPageViewModel.AutoStartEnabled));
    }

    [Fact]
    public void Demo_mode_never_reads_or_writes_auto_start()
    {
        var autoStart = new FakeAutoStart { IsAvailable = false, Enabled = true };
        var (f, vm, _, _) = Create(autoStart);
        using var _f = f;

        vm.AutoStartEnabled = true;
        vm.AutoStartEnabled = false;

        vm.AutoStartAvailable.Should().BeFalse();
        vm.AutoStartEnabled.Should().BeFalse();
        vm.AutoStartNote.Should().Be(BehaviorPageViewModel.AutoStartUnavailableNote);
        autoStart.Reads.Should().Be(0);
        autoStart.Writes.Should().BeEmpty();
    }

    [Fact]
    public void The_tray_icon_can_be_hidden_except_in_hidden_mode()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.TrayIconEditable.Should().BeTrue();
        vm.TrayIconVisible = false;
        f.Draft.Value.TrayIconVisible.Should().BeFalse();

        f.Draft.Edit(s => s with { Visibility = VisibilityMode.Hidden });
        f.Draft.Flush();

        vm.TrayIconVisible.Should().BeTrue();
        vm.TrayIconEditable.Should().BeFalse();
        vm.TrayIconNote.Should().Be(BehaviorPageViewModel.TrayLockedNote);

        vm.TrayIconVisible = false;
        f.Draft.HasPendingEdits.Should().BeFalse();
    }
}
