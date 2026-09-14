using FluentAssertions;
using UsageNotch.Hook;

namespace UsageNotch.Core.Tests.Hook;

public class AncestorPickerTests
{
    [Fact]
    public void A_shell_parent_is_skipped() =>
        AncestorPicker.Pick([(10, "cmd.exe"), (20, "node.exe")]).Should().Be(20);

    [Fact]
    public void Without_a_shell_the_parent_is_chosen() =>
        AncestorPicker.Pick([(10, "claude.exe"), (20, "explorer.exe")]).Should().Be(10);

    [Fact]
    public void When_every_ancestor_is_a_shell_the_parent_is_chosen() =>
        AncestorPicker.Pick([(10, "bash.exe"), (20, "sh.exe"), (30, "conhost.exe")]).Should().Be(10);

    [Fact]
    public void An_empty_chain_gives_zero() =>
        AncestorPicker.Pick([]).Should().Be(0);

    [Fact]
    public void Shell_names_are_compared_case_insensitively() =>
        AncestorPicker.Pick([(10, "BASH.EXE"), (20, "node.exe")]).Should().Be(20);

    [Fact]
    public void Several_shells_in_a_row_are_skipped() =>
        AncestorPicker.Pick([(10, "powershell.exe"), (20, "pwsh.exe"), (30, "claude.exe"), (40, "WindowsTerminal.exe")]).Should().Be(30);
}
