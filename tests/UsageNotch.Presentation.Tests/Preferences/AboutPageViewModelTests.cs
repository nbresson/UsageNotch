using FluentAssertions;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public class AboutPageViewModelTests
{
    private static SettingsEnvironment Env(bool demo = false) =>
        new("0.3.0", demo, @"C:\data", @"C:\data\logs", @"C:\data\settings.json", 48666, true);

    private static (DraftFixture F, AboutPageViewModel Vm, FakeShell Shell) Create(bool demo = false)
    {
        var f = new DraftFixture();
        var shell = new FakeShell();
        return (f, new AboutPageViewModel(f.Draft, shell, Env(demo)), shell);
    }

    [Fact]
    public void Shows_version_and_folders()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.VersionText.Should().Be("UsageNotch 0.3.0");
        vm.DataDirectory.Should().Be(@"C:\data");
        vm.LogsDirectory.Should().Be(@"C:\data\logs");
        vm.SettingsFile.Should().Be(@"C:\data\settings.json");
        vm.DemoHint.Should().BeEmpty();
    }

    [Fact]
    public void Demo_mode_says_where_test_data_lives()
    {
        var (f, vm, _) = Create(demo: true);
        using var _f = f;

        vm.DemoHint.Should().Be(@"Mode démo : réglages, lecture et journaux de test dans C:\data.");
    }

    [Fact]
    public void Debug_logging_edits_the_draft()
    {
        var (f, vm, _) = Create();
        using var _f = f;

        vm.DebugLogging = true;

        f.Draft.Value.DebugLogging.Should().BeTrue();
    }

    [Fact]
    public void Open_commands_open_the_folders_and_the_settings_file()
    {
        var (f, vm, shell) = Create();
        using var _f = f;

        vm.OpenDataFolderCommand.Execute(null);
        vm.OpenLogsFolderCommand.Execute(null);
        vm.OpenSettingsFileCommand.Execute(null);

        shell.Folders.Should().Equal(@"C:\data", @"C:\data\logs");
        shell.Files.Should().Equal(@"C:\data\settings.json");
    }

    [Fact]
    public void Doctor_errors_are_shown_and_cleared_on_success()
    {
        var (f, vm, shell) = Create();
        using var _f = f;

        shell.DoctorError = "Diagnostic impossible : disque plein";
        vm.RunDoctorCommand.Execute(null);
        vm.DoctorError.Should().Be("Diagnostic impossible : disque plein");

        shell.DoctorError = null;
        vm.RunDoctorCommand.Execute(null);
        vm.DoctorError.Should().BeEmpty();
        shell.DoctorRuns.Should().Be(2);
    }
}
