using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>Appelle le fournisseur toutes les 60 s pendant une session active, toutes les 5 min sinon ; jamais pendant un backoff sauf rafraîchissement forcé.</summary>
public sealed class UsagePoller : BackgroundService
{
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(5);

    private readonly IUsageProvider provider;
    private readonly UsageStore store;
    private readonly ISessionActivity activity;
    private readonly TimeProvider time;
    private readonly ILogger<UsagePoller> logger;
    private readonly SettingsStore? settings;
    private readonly BackoffPolicy _backoff = new();
    private readonly object _wakeGate = new();
    private volatile bool _forced;
    private CancellationTokenSource? _wake;
    private string _currentProvider;

    public UsagePoller(
        IUsageProvider provider,
        UsageStore store,
        ISessionActivity activity,
        TimeProvider time,
        ILogger<UsagePoller> logger,
        SettingsStore? settings = null)
    {
        this.provider = provider;
        this.store = store;
        this.activity = activity;
        this.time = time;
        this.logger = logger;
        this.settings = settings;
        _currentProvider = settings?.Current.Provider ?? provider.Id;

        if (settings is not null)
        {
            settings.Changed += OnSettingsChanged;
        }
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
        store.ClearBackoff();
        lock (_wakeGate) { _ = _wake?.CancelAsync(); }
    }

    public TimeSpan NextInterval() => activity.HasActiveSession ? ActiveInterval : IdleInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        store.Load();
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
                logger.LogError(e, "Échec du cycle d'usage, nouvel essai au prochain cycle");
            }
            await WaitAsync(NextInterval(), stoppingToken);
        }
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        if (store.IsInBackoff && !_forced) return;
        _forced = false;

        var result = await provider.FetchAsync(ct);
        switch (result)
        {
            case FetchResult.Success:
                _backoff.Reset();
                store.Apply(result);
                break;
            case FetchResult.RateLimited limited:
                store.Apply(result, _backoff.Next(limited.RetryAfter));
                break;
            default:
                store.Apply(result);
                break;
        }
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        if (_forced) return;

        // Pendant un backoff, se réveiller dès son échéance plutôt qu'au prochain intervalle.
        if (store.Current.BackoffUntil is { } until)
        {
            var remaining = until - time.GetUtcNow() + TimeSpan.FromSeconds(1);
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
            await Task.Delay(delay, time, linked.Token);
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
        if (settings is not null)
        {
            settings.Changed -= OnSettingsChanged;
        }
        base.Dispose();
    }
}
