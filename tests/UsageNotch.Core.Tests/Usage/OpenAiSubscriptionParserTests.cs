using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class OpenAiSubscriptionParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parses_complete_payload_with_primary_secondary_and_additional_limits()
    {
        var json = """
        {
            "plan_type": "plus",
            "rate_limit": {
                "primary_window": {
                    "used_percent": 25.0,
                    "limit_window_seconds": 18000,
                    "reset_after_seconds": 7200,
                    "reset_at": 1791208800
                },
                "secondary_window": {
                    "used_percent": 60.0,
                    "limit_window_seconds": 604800,
                    "reset_after_seconds": 86400,
                    "reset_at": 1791288000
                }
            },
            "additional_rate_limits": [
                {
                    "metering_type": "reasoning",
                    "title": "o1 & o3",
                    "rate_limit": {
                        "primary_window": {
                            "used_percent": 40.0,
                            "reset_after_seconds": 3600
                        }
                    }
                }
            ]
        }
        """;

        var windows = OpenAiSubscriptionParser.Parse(json, Now);

        windows.Should().HaveCount(3);

        var primary = windows.First(w => w.Id == "session");
        primary.Label.Should().Be("Session 5h");
        primary.UsedFraction.Should().BeApproximately(0.25, 0.001);
        primary.ResetsAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1791208800));

        var secondary = windows.First(w => w.Id == "weekly");
        secondary.Label.Should().Be("Quota hebdomadaire");
        secondary.UsedFraction.Should().BeApproximately(0.60, 0.001);
        secondary.ResetsAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1791288000));

        var reasoning = windows.First(w => w.Id == "reasoning_models");
        reasoning.Label.Should().Contain("o1 & o3");
        reasoning.UsedFraction.Should().BeApproximately(0.40, 0.001);
        reasoning.ResetsAt.Should().Be(Now.AddSeconds(3600));
    }

    [Fact]
    public void Computes_resets_at_from_reset_after_seconds_when_reset_at_is_missing()
    {
        var json = """
        {
            "rate_limit": {
                "primary_window": {
                    "used_percent": 10.0,
                    "reset_after_seconds": 1800
                },
                "secondary_window": {
                    "used_percent": 20.0,
                    "reset_after_seconds": 7200
                }
            }
        }
        """;

        var windows = OpenAiSubscriptionParser.Parse(json, Now);

        var primary = windows.First(w => w.Id == "session");
        primary.ResetsAt.Should().Be(Now.AddSeconds(1800));

        var secondary = windows.First(w => w.Id == "weekly");
        secondary.ResetsAt.Should().Be(Now.AddSeconds(7200));

        var reasoning = windows.First(w => w.Id == "reasoning_models");
        reasoning.UsedFraction.Should().Be(0.0);
    }

    [Fact]
    public void Handles_empty_or_malformed_json_gracefully()
    {
        var windows = OpenAiSubscriptionParser.Parse("{}", Now);
        windows.Should().HaveCount(3);
        windows[0].UsedFraction.Should().Be(0.0);
        windows[1].UsedFraction.Should().Be(0.0);
        windows[2].UsedFraction.Should().Be(0.0);

        var broken = OpenAiSubscriptionParser.Parse("{ not valid json", Now);
        broken.Should().HaveCount(3);
    }

    [Fact]
    public void Clamps_used_fraction_between_0_and_1()
    {
        var json = """
        {
            "rate_limit": {
                "primary_window": {
                    "used_percent": 150.0
                },
                "secondary_window": {
                    "used_percent": -10.0
                }
            }
        }
        """;

        var windows = OpenAiSubscriptionParser.Parse(json, Now);
        windows.First(w => w.Id == "session").UsedFraction.Should().Be(1.0);
        windows.First(w => w.Id == "weekly").UsedFraction.Should().Be(0.0);
    }
}
