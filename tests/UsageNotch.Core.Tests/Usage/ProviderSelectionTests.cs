using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ProviderSelectionTests
{
    [Theory]
    [InlineData("all", new[] { "claude", "antigravity", "openai" })]
    [InlineData("both", new[] { "claude", "antigravity" })]
    [InlineData("claude_openai", new[] { "claude", "openai" })]
    [InlineData("antigravity_openai", new[] { "antigravity", "openai" })]
    [InlineData("claude", new[] { "claude" })]
    [InlineData("antigravity", new[] { "antigravity" })]
    [InlineData("openai", new[] { "openai" })]
    [InlineData(null, new[] { "claude", "antigravity" })]
    [InlineData("", new[] { "claude", "antigravity" })]
    [InlineData("   ", new[] { "claude", "antigravity" })]
    public void Resolve_handles_standard_aliases(string? setting, string[] expected)
    {
        var resolved = ProviderSelection.Resolve(setting);
        resolved.Should().Equal(expected);
    }

    [Theory]
    [InlineData("claude,openai", new[] { "claude", "openai" })]
    [InlineData("openai+gemini", new[] { "openai", "antigravity" })]
    [InlineData("anthropic;chatgpt", new[] { "claude", "openai" })]
    [InlineData("claude | antigravity | openai", new[] { "claude", "antigravity", "openai" })]
    public void Resolve_handles_delimited_lists(string setting, string[] expected)
    {
        var resolved = ProviderSelection.Resolve(setting);
        resolved.Should().Equal(expected);
    }

    [Fact]
    public void Resolve_falls_back_to_claude_for_unrecognized_custom_input()
    {
        var resolved = ProviderSelection.Resolve("nonexistent_unknown_provider");
        resolved.Should().Equal(["claude"]);
    }

    [Theory]
    [InlineData("all", true)]
    [InlineData("both", true)]
    [InlineData("claude", true)]
    [InlineData("claude,openai", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("unknown_xyz", false)]
    public void IsValid_validates_settings_string(string? setting, bool expected)
    {
        ProviderSelection.IsValid(setting).Should().Be(expected);
    }
}
