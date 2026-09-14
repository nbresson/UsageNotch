namespace UsageNotch.Hook;

/// <summary>
/// Choisit le PID à transmettre à l'app : le premier ancêtre qui n'est pas un shell. Claude Code peut lancer
/// les hooks via cmd/bash/powershell, qui meurent avec le hook ; l'app doit partir d'un processus durable.
/// </summary>
public static class AncestorPicker
{
    private static readonly string[] Shells =
        ["cmd.exe", "bash.exe", "sh.exe", "powershell.exe", "pwsh.exe", "conhost.exe"];

    /// <param name="chain"><c>chain[0]</c> est le parent du hook, puis le grand-parent, etc.</param>
    /// <returns>PID du premier non-shell ; <c>chain[0]</c> si tous sont des shells ; 0 si la chaîne est vide.</returns>
    public static int Pick(IReadOnlyList<(int Pid, string ExeName)> chain)
    {
        if (chain.Count == 0) return 0;
        foreach (var (pid, exeName) in chain)
        {
            if (!IsShell(exeName)) return pid;
        }
        return chain[0].Pid;
    }

    private static bool IsShell(string exeName)
    {
        var name = Path.GetFileName(exeName ?? "");
        foreach (var shell in Shells)
        {
            if (string.Equals(name, shell, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
