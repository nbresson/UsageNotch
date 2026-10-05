using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Tests.Pill;

public class PillMetricsTests
{
    [Theory]
    [InlineData("claude", ScreenEdge.Top, CellContent.RingAndPercent, 104, 136)]
    [InlineData("claude", ScreenEdge.Right, CellContent.RingOnly, 104, 136)]
    [InlineData("antigravity", ScreenEdge.Top, CellContent.RingAndPercent, 104, 136)]
    [InlineData("both", ScreenEdge.Top, CellContent.RingAndPercent, 196, 228)]
    [InlineData("both", ScreenEdge.Bottom, CellContent.RingAndPercent, 196, 228)]
    [InlineData("both", ScreenEdge.Right, CellContent.RingAndPercent, 168, 200)]
    [InlineData("both", ScreenEdge.Left, CellContent.RingAndPercent, 168, 200)]
    [InlineData("both", ScreenEdge.Top, CellContent.RingOnly, 124, 156)]
    [InlineData("both", ScreenEdge.Right, CellContent.RingOnly, 124, 156)]
    public void Lengths_are_correctly_computed_for_all_modes_and_edges(
        string provider, ScreenEdge edge, CellContent content, double expectedBody, double expectedWindow)
    {
        PillMetrics.BodyLengthFor(provider, edge, content).Should().Be(expectedBody);
        PillMetrics.WindowLengthFor(provider, edge, content).Should().Be(expectedWindow);
    }
}
