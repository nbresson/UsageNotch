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
    [InlineData("openai", ScreenEdge.Top, CellContent.RingAndPercent, 104, 136)]
    [InlineData("both", ScreenEdge.Top, CellContent.RingAndPercent, 196, 228)]
    [InlineData("both", ScreenEdge.Bottom, CellContent.RingAndPercent, 196, 228)]
    [InlineData("both", ScreenEdge.Right, CellContent.RingAndPercent, 168, 200)]
    [InlineData("both", ScreenEdge.Left, CellContent.RingAndPercent, 168, 200)]
    [InlineData("both", ScreenEdge.Top, CellContent.RingOnly, 124, 156)]
    [InlineData("both", ScreenEdge.Right, CellContent.RingOnly, 124, 156)]
    [InlineData("claude_openai", ScreenEdge.Top, CellContent.RingAndPercent, 196, 228)]
    [InlineData("antigravity_openai", ScreenEdge.Right, CellContent.RingAndPercent, 168, 200)]
    [InlineData("all", ScreenEdge.Top, CellContent.RingAndPercent, 300, 332)]
    [InlineData("all", ScreenEdge.Bottom, CellContent.RingAndPercent, 300, 332)]
    [InlineData("all", ScreenEdge.Right, CellContent.RingAndPercent, 258, 290)]
    [InlineData("all", ScreenEdge.Left, CellContent.RingAndPercent, 258, 290)]
    [InlineData("all", ScreenEdge.Top, CellContent.RingOnly, 192, 224)]
    [InlineData("all", ScreenEdge.Right, CellContent.RingOnly, 192, 224)]
    public void Lengths_are_correctly_computed_for_all_modes_and_edges(
        string provider, ScreenEdge edge, CellContent content, double expectedBody, double expectedWindow)
    {
        PillMetrics.BodyLengthFor(provider, edge, content).Should().Be(expectedBody);
        PillMetrics.WindowLengthFor(provider, edge, content).Should().Be(expectedWindow);
    }

    [Theory]
    [InlineData("all", 3)]
    [InlineData("both", 2)]
    [InlineData("claude_openai", 2)]
    [InlineData("antigravity_openai", 2)]
    [InlineData("claude", 1)]
    [InlineData("antigravity", 1)]
    [InlineData("openai", 1)]
    [InlineData("unknown", 1)]
    public void CellCountFor_returns_correct_number_of_cells(string provider, int expectedCount)
    {
        PillMetrics.CellCountFor(provider).Should().Be(expectedCount);
    }
}
