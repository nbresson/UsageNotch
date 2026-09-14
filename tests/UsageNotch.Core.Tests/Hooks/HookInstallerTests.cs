using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Hooks;

namespace UsageNotch.Core.Tests.Hooks;

public class HookInstallerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static (HookInstaller Installer, string SettingsPath, string HookExe) Build(TempDir dir)
    {
        var hookExe = dir.File("UsageNotch.Hook.exe");
        File.WriteAllText(hookExe, "stub");
        var settings = Path.Combine(dir.Path, ".claude", "settings.json");
        return (new HookInstaller(settings, hookExe, new FakeTimeProvider(Now)), settings, hookExe);
    }

    private static JsonObject Load(string path) => (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

    [Fact]
    public void Install_creates_settings_with_the_seven_events()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, hookExe) = Build(dir);

        installer.Install();

        var hooks = (JsonObject)Load(settingsPath)["hooks"]!;
        hooks.Select(kv => kv.Key).Should().BeEquivalentTo(
            "SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "Notification", "Stop", "SessionEnd");

        var stop = (JsonObject)((JsonArray)hooks["Stop"]!)[0]!;
        stop["matcher"].Should().BeNull();
        var cmd = (JsonObject)((JsonArray)stop["hooks"]!)[0]!;
        cmd["type"]!.GetValue<string>().Should().Be("command");
        cmd["command"]!.GetValue<string>().Should().Be($"\"{hookExe}\" done");
        cmd["timeout"]!.GetValue<int>().Should().Be(5);

        var pre = (JsonObject)((JsonArray)hooks["PreToolUse"]!)[0]!;
        pre["matcher"]!.GetValue<string>().Should().Be("*");
        ((JsonObject)((JsonArray)pre["hooks"]!)[0]!)["command"]!.GetValue<string>().Should().EndWith(" running");

        installer.IsInstalled().Should().BeTrue();
    }

    [Fact]
    public void Install_preserves_third_party_hooks_and_other_settings()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "model": "opus",
          "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "other-tool notify" } ] } ] } }
        """);

        installer.Install();

        var root = Load(settingsPath);
        root["model"]!.GetValue<string>().Should().Be("opus");
        var stop = (JsonArray)root["hooks"]!["Stop"]!;
        stop.Should().HaveCount(2);
        ((JsonObject)((JsonArray)((JsonObject)stop[0]!)["hooks"]!)[0]!)["command"]!.GetValue<string>().Should().Be("other-tool notify");
    }

    [Fact]
    public void Installing_twice_replaces_our_entries_instead_of_duplicating_them()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);

        installer.Install();
        installer.Install();

        var stop = (JsonArray)Load(settingsPath)["hooks"]!["Stop"]!;
        stop.Should().HaveCount(1);
    }

    [Fact]
    public void Install_writes_a_timestamped_backup_when_a_file_existed()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "{ \"model\": \"opus\" }");

        installer.Install();

        var backup = Path.Combine(Path.GetDirectoryName(settingsPath)!, $"settings.json.usagenotch-bak-{Now.ToUnixTimeSeconds()}");
        File.Exists(backup).Should().BeTrue();
        File.ReadAllText(backup).Should().Be("{ \"model\": \"opus\" }");
    }

    [Fact]
    public void A_corrupt_settings_file_is_refused_and_left_untouched()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "{ corrupt");

        var act = () => installer.Install();

        act.Should().Throw<InvalidDataException>()
            .WithMessage("settings.json de Claude Code illisible — corrigez ou supprimez le fichier, puis réessayez.");
        File.ReadAllText(settingsPath).Should().Be("{ corrupt");
        Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, "settings.json.usagenotch-bak-*").Should().BeEmpty();
        installer.IsInstalled().Should().BeFalse();
    }

    [Fact]
    public void A_non_object_settings_file_is_refused()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "[1,2]");

        var act = () => installer.Install();

        act.Should().Throw<InvalidDataException>();
        File.ReadAllText(settingsPath).Should().Be("[1,2]");
        Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, "settings.json.usagenotch-bak-*").Should().BeEmpty();
    }

    [Theory]
    [InlineData("{ corrupt")]
    [InlineData("[1,2]")]
    public void Uninstall_on_an_unreadable_file_returns_a_message_and_writes_nothing(string content)
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, content);

        installer.Uninstall().Should().Contain("rien");

        File.ReadAllText(settingsPath).Should().Be(content);
        Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Should().ContainSingle();
    }

    [Fact]
    public void Trailing_commas_and_comments_are_tolerated_and_non_ascii_paths_stay_readable()
    {
        using var dir = new TempDir();
        var hookDir = Path.Combine(dir.Path, "Émile");
        Directory.CreateDirectory(hookDir);
        var hookExe = Path.Combine(hookDir, "UsageNotch.Hook.exe");
        File.WriteAllText(hookExe, "stub");
        var settingsPath = Path.Combine(dir.Path, ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        {
          // préférences
          "model": "opus",
        }
        """);
        var installer = new HookInstaller(settingsPath, hookExe, new FakeTimeProvider(Now));

        installer.Install();

        var text = File.ReadAllText(settingsPath);
        text.Should().Contain("Émile");
        Load(settingsPath)["model"]!.GetValue<string>().Should().Be("opus");
        installer.IsInstalled().Should().BeTrue();
    }

    [Fact]
    public void Install_fails_when_the_hook_executable_is_missing()
    {
        using var dir = new TempDir();
        var (_, settingsPath, _) = Build(dir);
        var installer = new HookInstaller(settingsPath, dir.File("missing.exe"), new FakeTimeProvider(Now));

        var act = () => installer.Install();

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Uninstall_removes_only_our_entries()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "other-tool notify" } ] } ] } }
        """);
        installer.Install();

        var message = installer.Uninstall();

        message.Should().Contain("7");
        var hooks = (JsonObject)Load(settingsPath)["hooks"]!;
        hooks.Select(kv => kv.Key).Should().Equal("Stop");
        ((JsonArray)hooks["Stop"]!).Should().HaveCount(1);
        installer.IsInstalled().Should().BeFalse();
    }

    [Fact]
    public void Uninstall_without_a_file_is_a_no_op()
    {
        using var dir = new TempDir();
        var (installer, _, _) = Build(dir);
        installer.Uninstall().Should().Contain("rien");
        installer.IsInstalled().Should().BeFalse();
    }

    [Fact]
    public void A_third_party_hook_that_mentions_our_name_is_kept()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "echo UsageNotch.Hook is great" } ] } ] } }
        """);

        installer.Install();

        var stopAfterInstall = (JsonArray)Load(settingsPath)["hooks"]!["Stop"]!;
        stopAfterInstall.Should().HaveCount(2);

        var message = installer.Uninstall();

        message.Should().Contain("7");
        var stopAfterUninstall = (JsonArray)Load(settingsPath)["hooks"]!["Stop"]!;
        stopAfterUninstall.Should().HaveCount(1);
        ((JsonObject)((JsonArray)((JsonObject)stopAfterUninstall[0]!)["hooks"]!)[0]!)["command"]!.GetValue<string>()
            .Should().Be("echo UsageNotch.Hook is great");
    }

    [Fact]
    public void Our_entries_from_an_older_install_folder_are_replaced()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, hookExe) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "\"C:\\Old\\UsageNotch.Hook.exe\" done" } ] } ] } }
        """);

        installer.Install();

        var stop = (JsonArray)Load(settingsPath)["hooks"]!["Stop"]!;
        stop.Should().HaveCount(1);
        ((JsonObject)((JsonArray)((JsonObject)stop[0]!)["hooks"]!)[0]!)["command"]!.GetValue<string>()
            .Should().Be($"\"{hookExe}\" done");
    }

    [Fact]
    public void Uninstall_preserves_other_top_level_settings()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "model": "opus", "theme": "dark" }
        """);

        installer.Install();
        installer.Uninstall();

        var root = Load(settingsPath);
        root["model"]!.GetValue<string>().Should().Be("opus");
        root["theme"]!.GetValue<string>().Should().Be("dark");
    }

    [Fact]
    public void Two_installs_in_the_same_second_keep_both_backups()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        const string original = "{ \"model\": \"opus\" }";
        File.WriteAllText(settingsPath, original);

        installer.Install();
        installer.Install();

        var settingsDir = Path.GetDirectoryName(settingsPath)!;
        var backups = Directory.GetFiles(settingsDir, "settings.json.usagenotch-bak-*");
        backups.Should().HaveCount(2);
        var firstBackup = Path.Combine(settingsDir, $"settings.json.usagenotch-bak-{Now.ToUnixTimeSeconds()}");
        File.Exists(firstBackup).Should().BeTrue();
        File.ReadAllText(firstBackup).Should().Be(original);
    }

    [Theory]
    [InlineData("\"C:\\x\\UsageNotch.Hook.exe\" done", true)]
    [InlineData("C:\\x\\UsageNotch.Hook.exe running", true)]
    [InlineData("\"C:\\x\\usagenotch.hook.EXE\" done", true)]
    [InlineData("echo UsageNotch.Hook.exe", false)]
    [InlineData("\"C:\\x\\UsageNotch.Hook.exe.bak\" done", false)]
    [InlineData("\"C:\\x\\UsageNotch.Hook.exe", false)]
    [InlineData("", false)]
    public void IsOurCommand_recognises_only_our_executable(string command, bool expected)
    {
        HookInstaller.IsOurCommand(command).Should().Be(expected);
    }
}
