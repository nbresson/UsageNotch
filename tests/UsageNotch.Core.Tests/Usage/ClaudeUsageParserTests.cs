using System.Text.Json;
using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ClaudeUsageParserTests
{
    private const string Reset1 = "2026-09-14T20:00:00Z";
    private const string Reset2 = "2026-09-18T00:00:00Z";

    [Fact]
    public void Reads_limits_array_and_puts_session_first()
    {
        var json = $$"""
        { "limits": [
            { "kind": "weekly_all", "percent": 7,  "resets_at": "{{Reset2}}" },
            { "kind": "session",    "percent": 73, "resets_at": "{{Reset1}}" }
        ] }
        """;

        var windows = ClaudeUsageParser.Parse(json);

        windows.Should().HaveCount(2);
        windows[0].Id.Should().Be("session");
        windows[0].Label.Should().Be("Session en cours");
        windows[0].UsedFraction.Should().BeApproximately(0.73, 1e-9);
        windows[0].ResetsAt.Should().Be(DateTimeOffset.Parse(Reset1));
        windows[1].Id.Should().Be("weekly_all");
        windows[1].Label.Should().Be("Hebdomadaire (tous modèles)");
    }

    [Fact]
    public void Skips_a_limit_without_reset_time()
    {
        var json = """{ "limits": [ { "kind": "session", "percent": 10 } ] }""";
        ClaudeUsageParser.Parse(json).Should().BeEmpty();
    }

    [Fact]
    public void Clamps_percent_into_0_1()
    {
        var json = $$"""{ "limits": [ { "kind": "session", "percent": 140, "resets_at": "{{Reset1}}" } ] }""";
        ClaudeUsageParser.Parse(json)[0].UsedFraction.Should().Be(1.0);
    }

    [Fact]
    public void Falls_back_to_five_hour_and_seven_day_when_limits_is_absent()
    {
        var json = $$"""
        { "five_hour": { "utilization": 42, "resets_at": "{{Reset1}}" },
          "seven_day": { "utilization": 9,  "resets_at": "{{Reset2}}" } }
        """;

        var windows = ClaudeUsageParser.Parse(json);

        windows.Select(w => w.Id).Should().Equal("session", "weekly_all");
        windows[0].UsedFraction.Should().BeApproximately(0.42, 1e-9);
    }

    [Fact]
    public void Does_not_duplicate_a_fallback_that_matches_a_limit_by_alias()
    {
        var json = $$"""
        { "limits": [ { "kind": "session", "percent": 73, "resets_at": "{{Reset1}}" } ],
          "five_hour": { "utilization": 73, "resets_at": "{{Reset1}}" } }
        """;
        ClaudeUsageParser.Parse(json).Should().HaveCount(1);
    }

    [Fact]
    public void Does_not_duplicate_a_fallback_that_matches_by_reset_and_value()
    {
        // weekly_scoped n'est pas un alias de seven_day, mais même reset et même valeur : doublon
        var json = $$"""
        { "limits": [ { "kind": "weekly_scoped", "percent": 9, "resets_at": "{{Reset2}}" } ],
          "seven_day": { "utilization": 9.2, "resets_at": "{{Reset2}}" } }
        """;
        ClaudeUsageParser.Parse(json).Should().HaveCount(1);
    }

    [Fact]
    public void Adds_the_fallback_when_it_is_genuinely_different()
    {
        var json = $$"""
        { "limits": [ { "kind": "weekly_opus", "percent": 30, "resets_at": "{{Reset2}}" } ],
          "seven_day": { "utilization": 9, "resets_at": "{{Reset2}}" } }
        """;
        ClaudeUsageParser.Parse(json).Select(w => w.Id).Should().Equal("weekly_opus", "weekly_all");
    }

    [Theory]
    [InlineData("seven_day_opus", "Hebdomadaire (Opus)")]
    [InlineData("weekly_opus", "Hebdomadaire (Opus)")]
    [InlineData("weekly_scoped", "Hebdomadaire (par modèle)")]
    [InlineData("some_new_kind", "Some new kind")]
    public void Labels_known_and_unknown_kinds(string kind, string label)
    {
        var json = $$"""{ "limits": [ { "kind": "{{kind}}", "percent": 1, "resets_at": "{{Reset1}}" } ] }""";
        ClaudeUsageParser.Parse(json)[0].Label.Should().Be(label);
    }

    [Fact]
    public void Throws_on_invalid_json()
    {
        var act = () => ClaudeUsageParser.Parse("{ not json");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Returns_empty_on_an_object_without_usage_fields()
    {
        ClaudeUsageParser.Parse("{}").Should().BeEmpty();
    }
}
