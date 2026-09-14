using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Sessions;

/// <summary>
/// Machine à quatre états par session. Done persiste jusqu'au prochain prompt, à un rejet manuel ou au balayage.
/// <see cref="Changed"/> n'est levé que si quelque chose de visible a changé.
/// </summary>
public sealed class SessionStore(TimeProvider time) : ISessionActivity
{
    public static readonly TimeSpan RunningStale = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan IdleDrop = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DoneStale = TimeSpan.FromHours(24);

    private const int PromptMax = 120;
    private const int CommandMax = 60;
    private const int MessageMax = 200;

    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public event Action? Changed;

    public bool HasActiveSession
    {
        get
        {
            lock (_gate) return _sessions.Values.Any(s => s.State is SessionState.Running or SessionState.Attention);
        }
    }

    public SessionState Aggregate
    {
        get
        {
            lock (_gate) return _sessions.Count == 0 ? SessionState.Idle : _sessions.Values.Max(s => s.State);
        }
    }

    public IReadOnlyList<Session> Snapshot()
    {
        lock (_gate)
        {
            return _sessions.Values
                .OrderByDescending(s => s.State)
                .ThenByDescending(s => s.Started)
                .ToList();
        }
    }

    public int? ParentPidOf(string id)
    {
        lock (_gate) return _sessions.TryGetValue(id, out var s) && s.ParentPid != 0 ? s.ParentPid : null;
    }

    public bool Apply(HookEvent ev)
    {
        bool changed;
        lock (_gate) changed = ApplyLocked(ev);
        if (changed) Changed?.Invoke();
        return changed;
    }

    public bool Dismiss(string id)
    {
        bool removed;
        lock (_gate) removed = _sessions.Remove(id);
        if (removed) Changed?.Invoke();
        return removed;
    }

    public bool Sweep()
    {
        var now = time.GetUtcNow();
        var changed = false;
        lock (_gate)
        {
            foreach (var (id, s) in _sessions.ToList())
            {
                if (s.State == SessionState.Running && now - s.LastEvent > RunningStale)
                {
                    // L'horloge d'Idle part de la bascule : sinon la session serait retirée dans le même balayage.
                    _sessions[id] = s with { State = SessionState.Idle, LastEvent = now };
                    changed = true;
                }
            }
            var before = _sessions.Count;
            foreach (var (id, s) in _sessions.ToList())
            {
                var drop = (s.State == SessionState.Idle && now - s.LastEvent > IdleDrop)
                        || (s.State == SessionState.Done && now - s.LastEvent > DoneStale);
                if (drop) _sessions.Remove(id);
            }
            changed |= _sessions.Count != before;
        }
        if (changed) Changed?.Invoke();
        return changed;
    }

    private bool ApplyLocked(HookEvent ev)
    {
        var now = time.GetUtcNow();
        if (ev.Kind == HookEvent.SessionEnd)
        {
            return _sessions.Remove(ev.SessionId);
        }

        var isNew = !_sessions.TryGetValue(ev.SessionId, out var s);
        s ??= new Session(ev.SessionId, TitleOf(ev.Cwd, ev.SessionId), SessionState.Idle, now, TimeSpan.Zero,
            "", "", "", "", 0, ev.Cwd, now);

        var before = (s.State, s.LastAction, s.AttentionMessage, s.Prompt, s.Model);

        s = s with { LastEvent = now };
        if (ev.ParentPid != 0) s = s with { ParentPid = ev.ParentPid };
        if (ev.Model.Length > 0) s = s with { Model = ev.Model };
        if (ev.Cwd.Length > 0 && s.Cwd.Length == 0) s = s with { Cwd = ev.Cwd, Title = TitleOf(ev.Cwd, s.Id) };

        switch (ev.Kind)
        {
            case HookEvent.SessionStart:
                if (s.State != SessionState.Running) s = s with { State = SessionState.Idle };
                break;
            case HookEvent.Running:
                if (s.State != SessionState.Running) s = s with { Started = now };
                s = s with { State = SessionState.Running, AttentionMessage = "" };
                if (ev.Prompt.Length > 0) s = s with { Prompt = Truncate(ev.Prompt, PromptMax) };
                if (ev.ToolName.Length > 0)
                {
                    s = s with
                    {
                        LastAction = ev.ToolCommand.Length == 0
                            ? $"🔧 {ev.ToolName}"
                            : $"🔧 {ev.ToolName} : {Truncate(ev.ToolCommand, CommandMax)}",
                    };
                }
                break;
            case HookEvent.Attention:
                s = s with { State = SessionState.Attention };
                if (ev.Message.Length > 0) s = s with { AttentionMessage = Truncate(ev.Message, MessageMax) };
                break;
            case HookEvent.Done:
                if (s.State != SessionState.Done) s = s with { Total = now - s.Started };
                s = s with { State = SessionState.Done, AttentionMessage = "" };
                break;
            default:
                break;
        }

        _sessions[ev.SessionId] = s;
        return isNew || before != (s.State, s.LastAction, s.AttentionMessage, s.Prompt, s.Model);
    }

    internal static string TitleOf(string cwd, string id)
    {
        var last = cwd.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "claude";
        var shortId = id.Length <= 4 ? id : id[..4];
        return $"{last} · {shortId}";
    }

    internal static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
