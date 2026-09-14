using FluentAssertions;

namespace UsageNotch.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void TempDir_creates_and_deletes_a_directory()
    {
        string path;
        using (var dir = new TempDir())
        {
            path = dir.Path;
            Directory.Exists(path).Should().BeTrue();
        }
        Directory.Exists(path).Should().BeFalse();
    }
}
