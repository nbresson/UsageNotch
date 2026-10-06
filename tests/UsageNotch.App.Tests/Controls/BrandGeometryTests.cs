using FluentAssertions;
using UsageNotch.App.Controls;

namespace UsageNotch.App.Tests.Controls;

public class BrandGeometryTests
{
    [Theory]
    [InlineData("claude")]
    [InlineData("antigravity")]
    [InlineData("openai")]
    [InlineData("unknown_fallback")]
    public void ForProvider_returns_frozen_non_empty_geometry(string providerId)
    {
        var geom = BrandGeometry.ForProvider(providerId);
        geom.Should().NotBeNull();
        geom.IsFrozen.Should().BeTrue();
        geom.Bounds.IsEmpty.Should().BeFalse();
    }
}
