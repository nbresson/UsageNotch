using System.Windows.Media;
using FluentAssertions;
using UsageNotch.App.Converters;
using UsageNotch.Core.Settings;

namespace UsageNotch.App.Tests.Converters;

public class ArrowGeometryConverterTests
{
    [Theory]
    [InlineData(ScreenEdge.Right)]
    [InlineData(ScreenEdge.Left)]
    [InlineData(ScreenEdge.Top)]
    [InlineData(ScreenEdge.Bottom)]
    public void Convert_returns_frozen_geometry_for_all_edges(ScreenEdge edge)
    {
        var conv = new ArrowGeometryConverter();
        var result = conv.Convert(edge, typeof(Geometry), null!, System.Globalization.CultureInfo.InvariantCulture);

        result.Should().BeOfType<StreamGeometry>();
        var geom = (StreamGeometry)result!;
        geom.IsFrozen.Should().BeTrue();
        geom.Bounds.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ConvertBack_throws_NotSupportedException()
    {
        var conv = new ArrowGeometryConverter();
        var act = () => conv.ConvertBack(null!, typeof(ScreenEdge), null!, System.Globalization.CultureInfo.InvariantCulture);
        act.Should().Throw<NotSupportedException>();
    }
}
