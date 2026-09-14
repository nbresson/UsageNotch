using FluentAssertions;
using UsageNotch.Hook;

namespace UsageNotch.Core.Tests.Hook;

public class PortReaderTests
{
    [Fact]
    public void Null_or_empty_gives_the_default_port()
    {
        PortReader.Read(null).Should().Be(48666);
        PortReader.Read("").Should().Be(48666);
    }

    [Theory]
    [InlineData("""{ "port": 5000 }""", 5000)]
    [InlineData("""{"scale":1,"port":48670,"edge":"Right"}""", 48670)]
    [InlineData("""{ "port" : 1234 }""", 1234)]
    public void Reads_the_port_key(string json, int expected) =>
        PortReader.Read(json).Should().Be(expected);

    [Theory]
    [InlineData("""{ "port": "abc" }""")]
    [InlineData("""{ "port": 99999 }""")]
    [InlineData("""{ "port": 80 }""")]
    [InlineData("""{ "portable": 7 }""")]
    [InlineData("""{ "scale": 1 }""")]
    [InlineData("not json at all")]
    public void Anything_else_gives_the_default_port(string json) =>
        PortReader.Read(json).Should().Be(48666);

    [Fact]
    public void Default_settings_path_is_under_appdata()
    {
        PortReader.DefaultSettingsPath.Should().EndWith(Path.Combine("UsageNotch", "settings.json"));
    }

    [Theory]
    [InlineData("""{ "port": 5000 }""", true)]
    [InlineData("""{ "autoLaunch": false }""", false)]
    [InlineData("""{"port":5000,"autoLaunch":false}""", false)]
    [InlineData("""{ "autoLaunch": true }""", true)]
    [InlineData("""{ "autoLaunchX": false }""", true)]
    [InlineData("""{ "autoLaunch": falsey }""", true)]
    [InlineData("not json at all", true)]
    public void Reads_the_auto_launch_flag(string json, bool expected) =>
        PortReader.ReadAutoLaunch(json).Should().Be(expected);

    [Fact]
    public void Auto_launch_defaults_to_true_without_settings()
    {
        PortReader.ReadAutoLaunch(null).Should().BeTrue();
        PortReader.ReadAutoLaunch("").Should().BeTrue();
    }
}
