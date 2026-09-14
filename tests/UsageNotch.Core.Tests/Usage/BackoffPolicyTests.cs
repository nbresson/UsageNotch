using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class BackoffPolicyTests
{
    [Fact]
    public void Doubles_from_60s_and_caps_at_15_minutes()
    {
        var policy = new BackoffPolicy();
        var waits = Enumerable.Range(0, 7).Select(_ => policy.Next(TimeSpan.Zero).TotalSeconds).ToList();
        waits.Should().Equal(60, 120, 240, 480, 900, 900, 900);
        policy.ConsecutiveFailures.Should().Be(7);
    }

    [Fact]
    public void Retry_after_only_raises_the_wait()
    {
        var policy = new BackoffPolicy();
        policy.Next(TimeSpan.FromSeconds(10)).TotalSeconds.Should().Be(60);
        policy.Next(TimeSpan.FromSeconds(1000)).TotalSeconds.Should().Be(1000);
    }

    [Fact]
    public void Reset_starts_over_at_60s()
    {
        var policy = new BackoffPolicy();
        policy.Next(TimeSpan.Zero);
        policy.Next(TimeSpan.Zero);
        policy.Reset();
        policy.ConsecutiveFailures.Should().Be(0);
        policy.Next(TimeSpan.Zero).TotalSeconds.Should().Be(60);
    }
}
