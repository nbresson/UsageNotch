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
    public void A_corrupt_settings_file_is_backed_up_and_replaced()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "{ corrupt");

        installer.Install();

        Load(settingsPath)["hooks"].Should().NotBeNull();
        Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, "settings.json.usagenotch-bak-*").Should().ContainSingle();
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
}
