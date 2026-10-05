using FluentAssertions;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Pill;

namespace UsageNotch.Presentation.Tests.Pill;

public class PillModelTests
{
    private static CellModel DummyCell(string providerId, ActivityKind activity = ActivityKind.None, string bandColor = "#123456") =>
        new([], "#000", "50 %", "#FFF", true, false, false, activity, "#FFF", "#888", bandColor, providerId);

    [Fact]
    public void Single_cell_pill_properties_behave_as_expected()
    {
        var cell = DummyCell("claude");
        var pill = new PillModel([cell], 104, 136, "#123456", ScreenEdge.Right);

        pill.IsDual.Should().BeFalse();
        pill.PrimaryCell.Should().BeSameAs(cell);
        pill.CellClaude.Should().BeSameAs(cell);
        pill.CellAntigravity.Should().BeNull();
        pill.BodyLength.Should().Be(104);
        pill.WindowLength.Should().Be(136);
        pill.BandColor.Should().Be("#123456");
        pill.Edge.Should().Be(ScreenEdge.Right);
    }

    [Fact]
    public void Dual_cell_pill_identifies_both_cells()
    {
        var claude = DummyCell("claude");
        var antigravity = DummyCell("antigravity");
        var pill = new PillModel([claude, antigravity], 196, 228, "#123456", ScreenEdge.Top);

        pill.IsDual.Should().BeTrue();
        pill.PrimaryCell.Should().BeSameAs(claude);
        pill.CellClaude.Should().BeSameAs(claude);
        pill.CellAntigravity.Should().BeSameAs(antigravity);
    }
}
