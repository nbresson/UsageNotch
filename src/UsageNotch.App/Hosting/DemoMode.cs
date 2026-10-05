using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;

namespace UsageNotch.App.Hosting;

/// <summary>Fournisseur de démo simulant les quotas de Claude ou d'Antigravity selon son identifiant.</summary>
public sealed class DemoUsageProvider : IUsageProvider
{
    private readonly string? _fixedId;
    private readonly SettingsStore? _settings;
    private readonly TimeProvider _time;

    public DemoUsageProvider(string providerId, TimeProvider time, SettingsStore? settings = null)
    {
        _fixedId = providerId;
        _time = time;
        _settings = settings;
    }

    public DemoUsageProvider(SettingsStore settings, TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    public string Id => _fixedId ?? (_settings?.Current.Provider == "antigravity" ? "antigravity" : (_settings?.Current.Provider == "openai" ? "openai" : "claude"));

    public string DisplayName => Id switch
    {
        "antigravity" => "Google Antigravity",
        "openai" => "OpenAI",
        _ => "Claude",
    };

    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => Id switch
    {
        "antigravity" => RingWindows.Antigravity,
        "openai" => RingWindows.OpenAi,
        _ => RingWindows.Claude,
    };

    public Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        IReadOnlyList<LimitWindow> windows = Id switch
        {
            "antigravity" =>
            [
                new("gemini-5h", "Modèles Gemini (5 h)", 0.65, now.AddHours(3).AddMinutes(12)),
                new("gemini-weekly", "Modèles Gemini (hebdomadaire)", 0.29, now.AddDays(4)),
                new("3p-weekly", "Modèles tiers (hebdomadaire)", 0.0, now.AddDays(6)),
            ],
            "openai" => _settings?.Current.OpenAiMode == "api"
                ?
                [
                    new("monthly_cost", "Budget mensuel (8.40 $ / 20 $)", 0.42, now.AddDays(18)),
                    new("daily_cost", "Consommation du jour (0.36 $)", 0.18, now.AddHours(14)),
                    new("reasoning_models", "Modèles raisonnement o1/o3 (6.50 $)", 0.65, now.AddDays(18)),
                ]
                :
                [
                    new("session", "Session 5h", 0.28, now.AddHours(3).AddMinutes(45)),
                    new("weekly", "Quota hebdomadaire", 0.54, now.AddDays(5)),
                    new("reasoning_models", "Modèles raisonnement (o1/o3)", 0.15, now.AddHours(3).AddMinutes(45)),
                ],
            _ =>
            [
                new("session", "Session en cours", 0.73, now.AddMinutes(51)),
                new("weekly_all", "Hebdomadaire (tous modèles)", 0.21, now.AddDays(3)),
                new("weekly_scoped", "Hebdomadaire (par modèle)", 0.52, now.AddDays(3)),
            ]
        };
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
