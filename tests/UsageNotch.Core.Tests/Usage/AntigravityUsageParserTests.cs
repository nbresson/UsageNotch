using System.Text.Json;
using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class AntigravityUsageParserTests
{
    private const string Reset5h = "2026-10-05T17:50:04Z";
    private const string ResetWeekly = "2026-10-10T10:44:30Z";
    private const string Reset3p = "2026-10-12T17:29:46Z";

    [Fact]
    public void Parses_full_antigravity_response_with_gemini_and_3p_groups()
    {
        var json = $$"""
        {
          "response": {
            "groups": [
              {
                "displayName": "Gemini Models",
                "buckets": [
                  {
                    "bucketId": "gemini-weekly",
                    "displayName": "Weekly Limit Remaining",
                    "window": "weekly",
                    "remainingFraction": 0.7128,
                    "resetTime": "{{ResetWeekly}}"
                  },
                  {
                    "bucketId": "gemini-5h",
                    "displayName": "Five Hour Limit Remaining",
                    "window": "5h",
                    "remainingFraction": 0.3529,
                    "resetTime": "{{Reset5h}}"
                  }
                ]
              },
              {
                "displayName": "Claude and GPT models",
                "buckets": [
                  {
                    "bucketId": "3p-weekly",
                    "displayName": "Weekly Limit Remaining",
                    "window": "weekly",
                    "remainingFraction": 1.0,
                    "resetTime": "{{Reset3p}}"
                  }
                ]
              }
            ]
          }
        }
        """;

        var windows = AntigravityUsageParser.Parse(json);

        windows.Should().HaveCount(3);

        // gemini-5h should be sorted first (short term / session)
        windows[0].Id.Should().Be("gemini-5h");
        windows[0].Label.Should().Be("Modèles Gemini (5 h)");
        windows[0].UsedFraction.Should().BeApproximately(0.6471, 1e-4);
        windows[0].ResetsAt.Should().Be(DateTimeOffset.Parse(Reset5h));

        // gemini-weekly second
        windows[1].Id.Should().Be("gemini-weekly");
        windows[1].Label.Should().Be("Modèles Gemini (hebdomadaire)");
        windows[1].UsedFraction.Should().BeApproximately(0.2872, 1e-4);
        windows[1].ResetsAt.Should().Be(DateTimeOffset.Parse(ResetWeekly));

        // 3p-weekly third
        windows[2].Id.Should().Be("3p-weekly");
        windows[2].Label.Should().Be("Modèles tiers (hebdomadaire)");
        windows[2].UsedFraction.Should().Be(0.0);
        windows[2].ResetsAt.Should().Be(DateTimeOffset.Parse(Reset3p));
    }

    [Fact]
    public void Parses_response_without_envelope_when_groups_are_at_root()
    {
        var json = $$"""
        {
          "groups": [
            {
              "displayName": "Gemini Models",
              "buckets": [
                {
                  "bucketId": "gemini-5h",
                  "remainingFraction": 0.5,
                  "resetTime": "{{Reset5h}}"
                }
              ]
            }
          ]
        }
        """;

        var windows = AntigravityUsageParser.Parse(json);
        windows.Should().HaveCount(1);
        windows[0].Id.Should().Be("gemini-5h");
        windows[0].UsedFraction.Should().Be(0.5);
    }

    [Fact]
    public void Clamps_used_fraction_between_zero_and_one()
    {
        var json = $$"""
        {
          "response": {
            "groups": [
              {
                "buckets": [
                  { "bucketId": "gemini-5h", "remainingFraction": -0.2, "resetTime": "{{Reset5h}}" },
                  { "bucketId": "gemini-weekly", "remainingFraction": 1.5, "resetTime": "{{ResetWeekly}}" }
                ]
              }
            ]
          }
        }
        """;

        var windows = AntigravityUsageParser.Parse(json);
        windows[0].UsedFraction.Should().Be(1.0); // 1 - (-0.2) = 1.2 => clamped to 1.0
        windows[1].UsedFraction.Should().Be(0.0); // 1 - 1.5 = -0.5 => clamped to 0.0
    }

    [Fact]
    public void Skips_buckets_without_reset_time_or_id()
    {
        var json = """
        {
          "response": {
            "groups": [
              {
                "buckets": [
                  { "bucketId": "", "remainingFraction": 0.5, "resetTime": "2026-10-05T17:50:04Z" },
                  { "bucketId": "gemini-5h", "remainingFraction": 0.5 }
                ]
              }
            ]
          }
        }
        """;

        AntigravityUsageParser.Parse(json).Should().BeEmpty();
    }

    [Fact]
    public void Throws_on_invalid_json()
    {
        var act = () => AntigravityUsageParser.Parse("{ not valid json");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Returns_empty_on_json_without_groups()
    {
        AntigravityUsageParser.Parse("{}").Should().BeEmpty();
    }

    [Theory]
    [InlineData("gemini-5h", "Modèles Gemini (5 h)")]
    [InlineData("gemini-weekly", "Modèles Gemini (hebdomadaire)")]
    [InlineData("3p-weekly", "Modèles tiers (hebdomadaire)")]
    [InlineData("3p-5h", "Modèles tiers (5 h)")]
    [InlineData("custom_bucket", "Custom bucket")]
    public void Labels_known_and_unknown_buckets(string bucketId, string expectedLabel)
    {
        var json = $$"""
        {
          "response": {
            "groups": [
              {
                "buckets": [
                  { "bucketId": "{{bucketId}}", "remainingFraction": 0.8, "resetTime": "{{Reset5h}}" }
                ]
              }
            ]
          }
        }
        """;

        AntigravityUsageParser.Parse(json)[0].Label.Should().Be(expectedLabel);
    }
}
