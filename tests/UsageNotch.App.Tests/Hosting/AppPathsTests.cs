using System.IO;
using FluentAssertions;
using UsageNotch.App.Hosting;

namespace UsageNotch.App.Tests.Hosting;

public class AppPathsTests
{
    [Fact]
    public void For_demo_mode_uses_isolated_temp_paths()
    {
        var paths = AppPaths.For(demo: true);

        paths.DataDirectory.Should().Contain("UsageNotch-demo");
        paths.SettingsFile.Should().EndWith("settings.json");
        paths.UsageFile.Should().EndWith("usage.json");
        paths.LogsDirectory.Should().EndWith("logs");
        paths.ClaudeSettingsFile.Should().EndWith("claude-settings.json");
    }

    [Fact]
    public void For_real_mode_uses_standard_appdata_paths()
    {
        var paths = AppPaths.For(demo: false);

        paths.DataDirectory.Should().Contain("UsageNotch");
        paths.ClaudeSettingsFile.Should().NotContain("UsageNotch-demo");
    }

    [Fact]
    public void EnsureDirectoriesCreated_creates_directories_safely()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "UsageNotchTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(
                DataDirectory: tempRoot,
                SettingsFile: Path.Combine(tempRoot, "settings.json"),
                UsageFile: Path.Combine(tempRoot, "usage.json"),
                LogsDirectory: Path.Combine(tempRoot, "logs"),
                HookExe: "UsageNotch.Hook.exe",
                ClaudeSettingsFile: Path.Combine(tempRoot, "claude.json"),
                NotificationsFile: Path.Combine(tempRoot, "notify.json"));

            Directory.Exists(paths.DataDirectory).Should().BeFalse();
            Directory.Exists(paths.LogsDirectory).Should().BeFalse();

            paths.EnsureDirectoriesCreated();

            Directory.Exists(paths.DataDirectory).Should().BeTrue();
            Directory.Exists(paths.LogsDirectory).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
