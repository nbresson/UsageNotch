using UsageNotch.Core.Sessions;
using UsageNotch.Core.Usage;

namespace UsageNotch.App.Hosting;

/// <summary>Lecture fixe : session 73 %, hebdomadaire 21 %, Opus 52 %.</summary>
public sealed class DemoUsageProvider(TimeProvider time) : IUsageProvider
{
    public string Id => "claude";
    public string DisplayName => "Claude";
    public string HeadlineWindowId => "session";

    public Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        IReadOnlyList<LimitWindow> windows =
        [
            new("session", "Session en cours", 0.73, now.AddMinutes(51)),
            new("weekly_all", "Hebdomadaire (tous modèles)", 0.21, now.AddDays(3)),
            new("weekly_opus", "Hebdomadaire (Opus)", 0.52, now.AddDays(3)),
        ];
        return Task.FromResult<FetchResult>(new FetchResult.Success(windows));
    }
}

public static class DemoMode
{
    public static readonly TimeSpan CycleInterval = TimeSpan.FromSeconds(20);

    /// <summary>Crée trois sessions (en cours, en attente, terminée) puis fait alterner la première.</summary>
    public static IDisposable Start(SessionStore sessions, TimeProvider time)
    {
        static HookEvent Ev(string kind, string id, string cwd, string message = "", string tool = "", string cmd = "") =>
            new(kind, id, 0, cwd, "", message, tool, cmd, "");

        sessions.Apply(Ev(HookEvent.SessionStart, "demo-api-0001", @"C:\src\api"));
        sessions.Apply(Ev(HookEvent.Running, "demo-api-0001", @"C:\src\api", tool: "Bash", cmd: "dotnet test"));
        sessions.Apply(Ev(HookEvent.SessionStart, "demo-web-0002", @"C:\src\web"));
        sessions.Apply(Ev(HookEvent.Running, "demo-web-0002", @"C:\src\web"));
        sessions.Apply(Ev(HookEvent.Attention, "demo-web-0002", @"C:\src\web", message: "Autoriser Bash : npm install ?"));
        sessions.Apply(Ev(HookEvent.SessionStart, "demo-doc-0003", @"C:\src\docs"));
        sessions.Apply(Ev(HookEvent.Running, "demo-doc-0003", @"C:\src\docs"));
        sessions.Apply(Ev(HookEvent.Done, "demo-doc-0003", @"C:\src\docs"));

        var running = true;
        return time.CreateTimer(_ =>
        {
            running = !running;
            sessions.Apply(Ev(running ? HookEvent.Running : HookEvent.Done, "demo-api-0001", @"C:\src\api", tool: "Bash", cmd: "dotnet test"));
        }, null, CycleInterval, CycleInterval);
    }
}
