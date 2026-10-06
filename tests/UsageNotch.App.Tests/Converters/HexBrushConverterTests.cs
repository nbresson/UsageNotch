using System.Windows.Data;
using System.Windows.Media;
using FluentAssertions;
using UsageNotch.App.Converters;

namespace UsageNotch.App.Tests.Converters;

public class HexBrushConverterTests
{
    [Theory]
    [InlineData("#FF0000", 255, 255, 0, 0)]
    [InlineData("#00FF00", 255, 0, 255, 0)]
    [InlineData("#0000FF", 255, 0, 0, 255)]
    [InlineData("#80FF0000", 128, 255, 0, 0)]
    public void ToBrush_parses_valid_hex_codes(string hex, byte a, byte r, byte g, byte b)
    {
        var brush = HexBrushConverter.ToBrush(hex);
        brush.Color.A.Should().Be(a);
        brush.Color.R.Should().Be(r);
        brush.Color.G.Should().Be(g);
        brush.Color.B.Should().Be(b);
        brush.IsFrozen.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-color")]
    [InlineData("#12")]
    [InlineData(null)]
    public void ToBrush_returns_transparent_on_invalid_or_null_hex(string? invalid)
    {
        var brush = HexBrushConverter.ToBrush(invalid);
        brush.Should().BeSameAs(Brushes.Transparent);
    }

    [Fact]
    public void ToBrush_caches_and_reuses_instances()
    {
        var brush1 = HexBrushConverter.ToBrush("#1A73E8");
        var brush2 = HexBrushConverter.ToBrush("#1A73E8");

        brush1.Should().BeSameAs(brush2);
        brush1.Color.R.Should().Be(0x1A);
        brush1.Color.G.Should().Be(0x73);
        brush1.Color.B.Should().Be(0xE8);
    }

    [Fact]
    public void Converter_Convert_and_ConvertBack()
    {
        var conv = new HexBrushConverter();
        var result = conv.Convert("#00FF00", typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();

        var back = conv.ConvertBack(result, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
        back.Should().Be(Binding.DoNothing);
    }
}
