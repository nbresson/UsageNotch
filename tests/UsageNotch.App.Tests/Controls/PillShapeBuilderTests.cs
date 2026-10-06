using System.Windows;
using FluentAssertions;
using UsageNotch.App.Controls;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Tests.Controls;

public class PillShapeBuilderTests
{
    [Theory]
    [InlineData(ScreenEdge.Right)]
    [InlineData(ScreenEdge.Left)]
    [InlineData(ScreenEdge.Top)]
    [InlineData(ScreenEdge.Bottom)]
    public void Pill_generates_valid_frozen_geometry(ScreenEdge edge)
    {
        var geom = PillShapeBuilder.Pill(edge, 64, 136, 16, 8);
        geom.Should().NotBeNull();
        geom.IsFrozen.Should().BeTrue();
        geom.Bounds.Width.Should().BeGreaterThan(0);
        geom.Bounds.Height.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(ScreenEdge.Right)]
    [InlineData(ScreenEdge.Left)]
    [InlineData(ScreenEdge.Top)]
    [InlineData(ScreenEdge.Bottom)]
    public void Body_computes_valid_rectangle(ScreenEdge edge)
    {
        var rect = PillShapeBuilder.Body(edge, 64, 136, 8);
        rect.Width.Should().BeGreaterThan(0);
        rect.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SlideOffset_computes_correct_vectors_for_all_edges()
    {
        var right = PillShapeBuilder.SlideOffset(ScreenEdge.Right, 64);
        right.X.Should().Be(64);
        right.Y.Should().Be(0);

        var left = PillShapeBuilder.SlideOffset(ScreenEdge.Left, 64);
        left.X.Should().Be(-64);
        left.Y.Should().Be(0);

        var top = PillShapeBuilder.SlideOffset(ScreenEdge.Top, 64);
        top.X.Should().Be(0);
        top.Y.Should().Be(-64);

        var bottom = PillShapeBuilder.SlideOffset(ScreenEdge.Bottom, 64);
        bottom.X.Should().Be(0);
        bottom.Y.Should().Be(64);
    }
}
