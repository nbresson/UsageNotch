namespace UsageNotch.Presentation.Behavior;

public sealed record TopLevelWindow(nint Handle, int ProcessId);

/// <summary>Choisit la fenêtre qui héberge une session à partir de l'arbre des processus. Logique pure, sans appel système.</summary>
public static class TerminalWindowChooser
{
    public const int MaxDepth = 8;

    /// <summary>Marge pour l'horloge et l'ordre des événements : le parent démarre toujours avant le premier hook.</summary>
    public static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(5);

    /// <summary>Un processus démarré après la session ne peut pas l'héberger : son numéro a été réutilisé.</summary>
    public static bool CanHostSession(DateTimeOffset processStarted, DateTimeOffset sessionStarted) =>
        processStarted <= sessionStarted + StartTolerance;

    public static IReadOnlyList<int> AncestorChain(int pid, IReadOnlyDictionary<int, int> parents)
    {
        var chain = new List<int> { pid };
        var current = pid;
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (!parents.TryGetValue(current, out var parent) || parent == 0 || chain.Contains(parent)) break;
            chain.Add(parent);
            current = parent;
        }
        return chain;
    }

    public static nint? Choose(int pid, IReadOnlyDictionary<int, int> parents, IReadOnlyList<TopLevelWindow> windows, int selfPid)
    {
        var chain = AncestorChain(pid, parents);
        nint? best = null;
        var bestIndex = int.MaxValue;

        foreach (var window in windows)
        {
            if (window.ProcessId == selfPid) continue;

            var index = IndexOf(chain, window.ProcessId);
            if (index < 0 && parents.TryGetValue(window.ProcessId, out var parent)) index = IndexOf(chain, parent);
            if (index < 0 || index >= bestIndex) continue;

            bestIndex = index;
            best = window.Handle;
        }
        return best;
    }

    private static int IndexOf(IReadOnlyList<int> chain, int pid)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            if (chain[i] == pid) return i;
        }
        return -1;
    }
}
