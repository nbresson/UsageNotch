using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class OpenAiCostParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Empty_or_invalid_json_produces_zeroed_windows()
    {
        var windows = OpenAiCostParser.Parse("{}", 30.0, Now);

        windows.Should().HaveCount(3);
        windows[0].Id.Should().Be("monthly_cost");
        windows[0].UsedFraction.Should().Be(0.0);
        windows[1].Id.Should().Be("daily_cost");
        windows[1].UsedFraction.Should().Be(0.0);
        windows[2].Id.Should().Be("reasoning_models");
        windows[2].UsedFraction.Should().Be(0.0);
    }

    [Fact]
    public void Parses_amounts_and_categorizes_today_and_reasoning_models()
    {
        var todayTs = Now.ToUnixTimeSeconds();
        var yesterdayTs = Now.AddDays(-1).ToUnixTimeSeconds();

        var json = $$"""
        {
          "object": "list",
          "data": [
            {
              "amount": 10.0,
              "start_time": {{yesterdayTs}},
              "line_item": "gpt-4o"
            },
            {
              "amount": { "value": 5.0 },
              "start_time": {{todayTs}},
              "line_item": "gpt-4o-mini"
            },
            {
              "amount": 15.0,
              "start_time": {{todayTs}},
              "line_item": "o1-preview"
            }
          ]
        }
        """;

        // monthlyBudget = 100.0 => dailyBudget = 100/30 = 3.333..., reasoningBudget = 50.0
        var windows = OpenAiCostParser.Parse(json, 100.0, Now);

        windows.Should().HaveCount(3);

        // Monthly: 10 + 5 + 15 = 30.0 / 100.0 = 0.3
        windows[0].Id.Should().Be("monthly_cost");
        windows[0].UsedFraction.Should().BeApproximately(0.3, 0.001);
        windows[0].Label.Should().Contain("30.00 $ / 100 $");

        // Daily: today has 5.0 + 15.0 = 20.0. Budget is 100/30 = 3.333, so 20.0 > 3.333 => clamped to 1.0
        windows[1].Id.Should().Be("daily_cost");
        windows[1].UsedFraction.Should().Be(1.0);
        windows[1].Label.Should().Contain("20.00 $");

        // Reasoning: 15.0 / 50.0 = 0.3
        windows[2].Id.Should().Be("reasoning_models");
        windows[2].UsedFraction.Should().BeApproximately(0.3, 0.001);
        windows[2].Label.Should().Contain("15.00 $");
    }

    [Fact]
    public void Detects_timestamp_and_model_fields_alternative()
    {
        var todayTs = Now.ToUnixTimeSeconds();
        var json = $$"""
        {
          "data": [
            {
              "amount": 2.0,
              "timestamp": {{todayTs}},
              "model": "o3-mini"
            }
          ]
        }
        """;

        var windows = OpenAiCostParser.Parse(json, 20.0, Now);

        windows[0].UsedFraction.Should().BeApproximately(0.1, 0.001); // 2 / 20 = 0.1
        windows[1].UsedFraction.Should().Be(1.0); // 2.0 / 1.0 (dailyBudget = max(1, 20/30) = 1.0) => 1.0
        windows[2].UsedFraction.Should().BeApproximately(0.2, 0.001); // 2.0 / 10.0 = 0.2
    }

    [Fact]
    public void Fallback_budget_applies_when_budget_is_invalid()
    {
        var windows = OpenAiCostParser.Parse("{}", 0.0, Now);
        windows[0].Label.Should().Contain("20 $");
    }
}
