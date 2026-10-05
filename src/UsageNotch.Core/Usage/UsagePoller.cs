using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>Appelle les fournisseurs d'usage toutes les 60 s pendant une session active, toutes les 5 min sinon ; jamais pendant un backoff sauf rafraîchissement forcé.</summary>
public sealed class UsagePoller : BackgroundService
{
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<IUsageProvider> _providers;
    private readonly UsageStore _store;
    private readonly ISessionActivity _activity;
    private readonly TimeProvider _time;
    private readonly ILogger<UsagePoller> _logger;
    private readonly SettingsStore? _settings;
    private readonly Dictionary<string, BackoffPolicy> _backoffs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _wakeGate = new();
    private volatile bool _forced;
    private CancellationTokenSource? _wake;
    private string _currentProvider;

    public UsagePoller(
        IReadOnlyList<IUsageProvider> providers,
        UsageStore store,
        ISessionActivity activity,
        TimeProvider time,
        ILogger<UsagePoller> logger,
        SettingsStore? settings = null)
    {
        _providers = providers;
        _store = store;
        _activity = activity;
        _time = time;
        _logger = logger;
        _settings = settings;
        _currentProvider = settings?.Current.Provider ?? (providers.Count > 0 ? providers[0].Id : "claude");

        foreach (var p in providers)
        {
            _backoffs[p.Id] = new BackoffPolicy();
        }

        if (settings is not null)
        {
            settings.Changed += OnSettingsChanged;
        }
    }

    public UsagePoller(
        IUsageProvider provider,
        UsageStore store,
        ISessionActivity activity,
        TimeProvider time,
        ILogger<UsagePoller> logger,
        SettingsStore? settings = null)
        : this([provider], store, activity, time, logger, settings)
    {
    }

    private void OnSettingsChanged(CoreSettings s)
    {
        if (s.Provider != _currentProvider)
        {
            _currentProvider = s.Provider;
            RequestRefresh();
        }
    }

    /// <summary>« Rafraîchir maintenant » : efface le backoff et interrompt l'attente en cours.</summary>
    public void RequestRefresh()
    {
        _forced = true;
        _store.ClearBackoff();
        lock (_wakeGate) { _ = _wake?.CancelAsync(); }
    }

    public TimeSpan NextInterval() => _activity.HasActiveSession ? ActiveInterval : IdleInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _store.Load();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Échec du cycle d'usage, nouvel essai au prochain cycle");
            }
            await WaitAsync(NextInterval(), stoppingToken);
        }
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        var forced = _forced;
        _forced = false;

        var active = GetActiveProviders();
        foreach (var provider in active)
        {
            if (_store.IsInBackoffFor(provider.Id) && !forced)
            {
                continue;
            }

            try
            {
                var result = await provider.FetchAsync(ct);
                var backoff = GetBackoff(provider.Id);

                switch (result)
                {
                    case FetchResult.Success:
                        backoff.Reset();
                        _store.Apply(provider.Id, result);
                        break;
                    case FetchResult.RateLimited limited:
                        _store.Apply(provider.Id, result, backoff.Next(limited.RetryAfter));
                        break;
                    default:
                        _store.Apply(provider.Id, result);
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la lecture du fournisseur {ProviderId}", provider.Id);
                _store.Apply(provider.Id, new FetchResult.Failed("Erreur inattendue"));
            }
        }
    }

    private IReadOnlyList<IUsageProvider> GetActiveProviders()
    {
        if (_settings is null || _providers.Count <= 1) return _providers;
        var mode = _settings.Current.Provider;

        var selected = mode switch
        {
            "all" => _providers,
            "both" => _providers.Where(p => string.Equals(p.Id, "claude", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Id, "antigravity", StringComparison.OrdinalIgnoreCase)).ToList(),
            "claude_openai" => _providers.Where(p => string.Equals(p.Id, "claude", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Id, "openai", StringComparison.OrdinalIgnoreCase)).ToList(),
            "antigravity_openai" => _providers.Where(p => string.Equals(p.Id, "antigravity", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Id, "openai", StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => _providers.Where(p => string.Equals(p.Id, mode, StringComparison.OrdinalIgnoreCase)).ToList()
        };

        return selected.Count > 0 ? selected : _providers;
    }

    private BackoffPolicy GetBackoff(string providerId)
    {
        if (!_backoffs.TryGetValue(providerId, out var policy))
        {
            policy = new BackoffPolicy();
            _backoffs[providerId] = policy;
        }
        return policy;
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        if (_forced) return;

        // Pendant un backoff, se réveiller dès son échéance la plus proche plutôt qu'au prochain intervalle.
        var active = GetActiveProviders();
        var earliestUntil = active
            .Select(p => _store.SnapshotFor(p.Id).BackoffUntil)
            .Where(until => until is not null)
            .Min();

        if (earliestUntil is { } until)
        {
            var remaining = until - _time.GetUtcNow() + TimeSpan.FromSeconds(1);
            if (remaining > TimeSpan.Zero && remaining < delay) delay = remaining;
        }

        using var wake = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, wake.Token);
        lock (_wakeGate)
        {
            if (_forced) return; // un rafraîchissement arrivé après la première vérification ne doit pas être perdu
            _wake = wake;
        }
        try
        {
            await Task.Delay(delay, _time, linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Réveil par RequestRefresh : on repart tout de suite.
        }
        finally
        {
            lock (_wakeGate) { _wake = null; }
        }
    }

    public override void Dispose()
    {
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
        }
        base.Dispose();
    }
}
