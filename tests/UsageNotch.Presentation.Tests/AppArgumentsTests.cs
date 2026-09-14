using FluentAssertions;

namespace UsageNotch.Presentation.Tests;

public class AppArgumentsTests
{
    [Fact]
    public void No_arguments_is_a_normal_start() =>
        AppArguments.Parse([]).Should().Be(new AppArguments(false, false, false));

    [Theory]
    [InlineData("doctor")]
    [InlineData("--doctor")]
    [InlineData("DOCTOR")]
    public void Doctor_is_recognised(string arg) => AppArguments.Parse([arg]).Doctor.Should().BeTrue();

    [Fact]
    public void Demo_and_from_hook_are_flags_and_unknown_arguments_are_ignored() =>
        AppArguments.Parse(["--demo", "--whatever", "--FROM-HOOK"]).Should().Be(new AppArguments(false, true, true));
}
