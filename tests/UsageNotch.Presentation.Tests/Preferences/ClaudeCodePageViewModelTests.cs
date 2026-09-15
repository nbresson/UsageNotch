using FluentAssertions;
using UsageNotch.Presentation.Preferences;
using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests.Preferences;

public class ClaudeCodePageViewModelTests
{
    private static SettingsEnvironment Env(int port = 48666, bool listening = true, bool demo = false) =>
        new("0.3.0", demo, @"C:\data", @"C:\data\logs", @"C:\data\settings.json", port, listening);

    private static (DraftFixture F, ClaudeCodePageViewModel Vm, FakeHooks Hooks, FakeShell Shell) Create(
        FakeHooks? hooks = null, SettingsEnvironment? env = null)
    {
        var f = new DraftFixture();
        hooks ??= new FakeHooks();
        var shell = new FakeShell();
        return (f, new ClaudeCodePageViewModel(f.Draft, hooks, shell, env ?? Env()), hooks, shell);
    }

    [Fact]
    public void Shows_the_hook_status_and_paths()
    {
        var (f, vm, hooks, _) = Create();
        using var _f = f;

        vm.HooksInstalled.Should().BeFalse();
        vm.HooksStatus.Should().Be("Non installés");
        vm.ClaudeSettingsPath.Should().Be(hooks.SettingsPath);
        vm.HookExePath.Should().Be(hooks.HookExePath);
        vm.HookExeStatus.Should().Be("Présent");
        vm.LastMessage.Should().BeEmpty();
        vm.PortText.Should().Be("48666");
        vm.PortNote.Should().BeEmpty();
        vm.DemoHint.Should().BeEmpty();
    }

    [Fact]
    public void Install_writes_the_hooks_and_reports_the_message()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.InstallCommand.CanExecute(null).Should().BeTrue();
        vm.UninstallCommand.CanExecute(null).Should().BeFalse();

        vm.InstallCommand.Execute(null);

        vm.HooksInstalled.Should().BeTrue();
        vm.HooksStatus.Should().Be("Installés");
        vm.LastMessage.Should().Be("7 hooks écrits");
        vm.LastActionFailed.Should().BeFalse();
        vm.InstallCommand.CanExecute(null).Should().BeFalse();
        vm.UninstallCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void A_failed_install_shows_the_error_and_keeps_the_status()
    {
        var hooks = new FakeHooks { NextResult = new HookSetupResult(false, "Impossible de modifier les hooks Claude Code : fichier illisible") };
        var (f, vm, _, _) = Create(hooks);
        using var _f = f;

        vm.InstallCommand.Execute(null);

        vm.HooksInstalled.Should().BeFalse();
        vm.LastActionFailed.Should().BeTrue();
        vm.LastMessage.Should().Be("Impossible de modifier les hooks Claude Code : fichier illisible");
    }

    [Fact]
    public void Install_is_disabled_without_the_hook_executable()
    {
        var (f, vm, _, _) = Create(new FakeHooks { HookExeExists = false });
        using var _f = f;

        vm.InstallCommand.CanExecute(null).Should().BeFalse();
        vm.HookExeStatus.Should().Be("Introuvable : les hooks ne peuvent pas être installés.");
    }

    [Fact]
    public void Uninstall_removes_the_hooks()
    {
        var (f, vm, _, _) = Create(new FakeHooks { Installed = true });
        using var _f = f;

        vm.UninstallCommand.Execute(null);

        vm.HooksInstalled.Should().BeFalse();
        vm.LastMessage.Should().Be("7 hooks retirés");
    }

    [Fact]
    public void Refresh_rereads_the_hook_status()
    {
        var (f, vm, hooks, _) = Create();
        using var _f = f;

        hooks.Installed = true;
        vm.RefreshCommand.Execute(null);

        vm.HooksInstalled.Should().BeTrue();
    }

    [Fact]
    public void A_valid_port_edits_the_draft_and_announces_a_restart()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PortText = " 48700 ";

        f.Draft.Value.Port.Should().Be(48700);
        vm.PortError.Should().BeEmpty();
        vm.PortNote.Should().Be(ClaudeCodePageViewModel.RestartNote);
    }

    [Theory]
    [InlineData("80")]
    [InlineData("70000")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-48666")]
    public void An_invalid_port_shows_an_error_and_changes_nothing(string text)
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PortText = text;

        vm.PortError.Should().Be(ClaudeCodePageViewModel.PortRangeError);
        vm.PortText.Should().Be(text);
        f.Draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void Going_back_to_the_listening_port_clears_the_note()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        vm.PortText = "48700";
        vm.PortText = "48666";

        vm.PortNote.Should().BeEmpty();
        f.Draft.Value.Port.Should().Be(48666);
    }

    [Fact]
    public void An_unavailable_listening_port_is_explained()
    {
        var (f, vm, _, _) = Create(env: Env(listening: false));
        using var _f = f;

        vm.PortNote.Should().Be("Le port 48666 est indisponible : une autre application l'utilise peut-être. Choisissez-en un autre, puis redémarrez UsageNotch.");
    }

    [Fact]
    public void An_outside_port_change_updates_the_text()
    {
        var (f, vm, _, _) = Create();
        using var _f = f;

        f.Store.Save(f.Store.Current with { Port = 49000 });

        vm.PortText.Should().Be("49000");
    }

    [Fact]
    public void Demo_mode_says_hooks_go_to_a_test_file()
    {
        var (f, vm, _, _) = Create(env: Env(demo: true));
        using var _f = f;

        vm.DemoHint.Should().Be(ClaudeCodePageViewModel.DemoNote);
    }

    [Fact]
    public void Open_folder_opens_the_claude_settings_directory()
    {
        var (f, vm, _, shell) = Create();
        using var _f = f;

        vm.OpenClaudeFolderCommand.Execute(null);

        shell.Folders.Should().Equal(@"C:\Users\test\.claude");
    }
}
