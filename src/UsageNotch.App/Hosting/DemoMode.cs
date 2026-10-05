using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;

namespace UsageNotch.App.Hosting;

/// <summary>Fournisseur de démo simulant les quotas de Claude ou d'Antigravity selon les réglages.</summary>
public sealed class DemoUsageProvider(SettingsStore settings, TimeProvider time) : IUsageProvider
{
    public string Id => settings.Current.Provider;

    public string DisplayName => settings.Current.Provider == "antigravity" ? "Google Antigravity" : "Claude";

    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds =>
        settings.Current.Provider == "antigravity" ? RingWindows.Antigravity : RingWindows.Claude;

    public Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        IReadOnlyList<LimitWindow> windows = settings.Current.Provider == "antigravity"
            ?
            [
                new("gemini-5h", "Modèles Gemini (5 h)", 0.65, now.AddHours(3).AddMinutes(12)),
                new("gemini-weekly", "Modèles Gemini (hebdomadaire)", 0.29, now.AddDays(4)),
                new("3p-weekly", "Modèles tiers (hebdomadaire)", 0.0, now.AddDays(6)),
            ]
            :
            [
                new("session", "Session en cours", 0.73, now.AddMinutes(51)),
                new("weekly_all", "Hebdomadaire (tous modèles)", 0.21, now.AddDays(3)),
                new("weekly_scoped", "Hebdomadaire (par modèle)", 0.52, now.AddDays(3)),
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

        // La session docs se termine au premier cycle (un Done au même instant que Running afficherait « Terminé en 0 s »).
        var running = true;
        var docsDone = false;
        return time.CreateTimer(_ =>
        {
            if (!docsDone)
            {
                docsDone = true;
                sessions.Apply(Ev(HookEvent.Done, "demo-doc-0003", @"C:\src\docs"));
            }
            running = !running;
            sessions.Apply(Ev(running ? HookEvent.Running : HookEvent.Done, "demo-api-0001", @"C:\src\api", tool: "Bash", cmd: "dotnet test"));
        }, null, CycleInterval, CycleInterval);
    }
}
