using UsageNotch.Core.Sessions;

namespace UsageNotch.Presentation.Behavior;

public enum TransitionKind
{
    Done,
    Attention,
}

public sealed record SessionTransition(string SessionId, string Title, TransitionKind Kind);

/// <summary>
/// Compare chaque instantané des sessions au précédent et rend les passages vers Done ou Attention.
/// Une session inconnue est enregistrée sans être annoncée. Non thread-safe : appeler depuis le thread UI.
/// </summary>
public sealed class TransitionWatcher
{
    private Dictionary<string, SessionState> _last = new(StringComparer.Ordinal);

    public IReadOnlyList<SessionTransition> Observe(IReadOnlyList<Session> sessions)
    {
        var next = new Dictionary<string, SessionState>(StringComparer.Ordinal);
        var transitions = new List<SessionTransition>();

        foreach (var s in sessions)
        {
            next[s.Id] = s.State;
            if (!_last.TryGetValue(s.Id, out var previous)) continue;

            if (s.State == SessionState.Done && previous != SessionState.Done)
            {
                transitions.Add(new SessionTransition(s.Id, s.Title, TransitionKind.Done));
            }
            else if (s.State == SessionState.Attention && previous != SessionState.Attention)
            {
                transitions.Add(new SessionTransition(s.Id, s.Title, TransitionKind.Attention));
            }
        }

        _last = next;
        return transitions;
    }
}
