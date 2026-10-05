using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Compose le snapshot publié à partir des résultats d'appel pour chaque fournisseur : conserve la dernière bonne lecture,
/// la marque Stale en cas d'échec, persiste le tout (échéance de backoff comprise) et publie <see cref="Changed"/>.
/// </summary>
public sealed class UsageStore(string filePath, TimeProvider time, ILogger<UsageStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private readonly Dictionary<string, UsageSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Snapshot du fournisseur par défaut (« claude ») pour la compatibilité ascendante.</summary>
    public UsageSnapshot Current => SnapshotFor("claude");

    /// <summary>Tous les snapshots en mémoire.</summary>
    public IReadOnlyDictionary<string, UsageSnapshot> Snapshots
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, UsageSnapshot>(_snapshots, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Levé sur un thread d'arrière-plan (celui du poller), éventuellement dans le désordre : l'abonné doit relire
    /// <see cref="Current"/> ou <see cref="SnapshotFor"/> sur le thread UI plutôt que se fier à l'argument reçu.
    /// </summary>
    public event Action<UsageSnapshot>? Changed;

    /// <summary>Vrai si au moins un fournisseur est en période de backoff.</summary>
    public bool IsInBackoff =>
        Snapshots.Values.Any(s => s.BackoffUntil is { } until && until > time.GetUtcNow());

    /// <summary>Vrai si le fournisseur désigné est en période de backoff.</summary>
    public bool IsInBackoffFor(string providerId) =>
        SnapshotFor(providerId).BackoffUntil is { } until && until > time.GetUtcNow();

    public UsageSnapshot SnapshotFor(string providerId)
    {
        lock (_gate)
        {
            return _snapshots.TryGetValue(providerId, out var snap) ? snap : UsageSnapshot.Empty;
        }
    }

    /// <summary>Recharge la lecture persistée. Une vieille valeur vaut mieux qu'un anneau vide : elle est publiée en Stale.</summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var text = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(text);

            lock (_gate)
            {
                // Si la racine porte une propriété "status" ou "windows", c'est un ancien fichier monobloc
                if (doc.RootElement.TryGetProperty("status", out _) || doc.RootElement.TryGetProperty("windows", out _))
                {
                    var saved = JsonSerializer.Deserialize<UsageSnapshot>(text, JsonOptions);
                    if (saved is not null)
                    {
                        var restored = saved.Windows.Count > 0
                            ? saved with { Status = SnapshotStatus.Stale }
                            : saved with { Status = saved.Status == SnapshotStatus.Ok ? SnapshotStatus.Error : saved.Status };
                        _snapshots["claude"] = restored;
                    }
                }
                else
                {
                    // Nouveau format dictionnaire
                    var dict = JsonSerializer.Deserialize<Dictionary<string, UsageSnapshot>>(text, JsonOptions);
                    if (dict is not null)
                    {
                        foreach (var (providerId, saved) in dict)
                        {
                            var restored = saved.Windows.Count > 0
                                ? saved with { Status = SnapshotStatus.Stale }
                                : saved with { Status = saved.Status == SnapshotStatus.Ok ? SnapshotStatus.Error : saved.Status };
                            _snapshots[providerId] = restored;
                        }
                    }
                }
            }
            Changed?.Invoke(Current);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Lecture de {Path} impossible, on repart de zéro", filePath);
        }
    }

    public void Apply(FetchResult result, TimeSpan backoffWait = default) =>
        Apply("claude", result, backoffWait);

    public void Apply(string providerId, FetchResult result, TimeSpan backoffWait = default)
    {
        var now = time.GetUtcNow();
        var prev = SnapshotFor(providerId);
        var hasReading = prev.Windows.Count > 0;

        UsageSnapshot next = result switch
        {
            FetchResult.Success s => new UsageSnapshot(SnapshotStatus.Ok, s.Windows, now, "", null),
            FetchResult.NeedsAuth n => prev with { Status = SnapshotStatus.NeedsAuth, Note = n.Note, BackoffUntil = null },
            FetchResult.RateLimited => prev with
            {
                Status = hasReading ? SnapshotStatus.Stale : SnapshotStatus.Backoff,
                Note = $"Limite d'appels atteinte, nouvel essai dans {(int)backoffWait.TotalSeconds} s",
                BackoffUntil = now + backoffWait,
            },
            FetchResult.Failed f => prev with
            {
                Status = hasReading ? SnapshotStatus.Stale : SnapshotStatus.Error,
                Note = f.Note,
                BackoffUntil = null,
            },
            _ => prev,
        };

        Publish(providerId, next, persist: true);
    }

    /// <summary>« Rafraîchir maintenant » : l'échéance de backoff est oubliée pour que le prochain cycle appelle tout de suite.</summary>
    public void ClearBackoff()
    {
        var hadAny = false;
        lock (_gate)
        {
            foreach (var (providerId, snap) in _snapshots.ToList())
            {
                if (snap.BackoffUntil is not null)
                {
                    hadAny = true;
                    var next = snap with
                    {
                        BackoffUntil = null,
                        Status = snap.Windows.Count > 0 ? SnapshotStatus.Stale : SnapshotStatus.Error,
                    };
                    _snapshots[providerId] = next;
                }
            }
            if (hadAny)
            {
                Persist();
            }
        }
        if (hadAny)
        {
            Changed?.Invoke(Current);
        }
    }

    private void Publish(string providerId, UsageSnapshot next, bool persist)
    {
        lock (_gate)
        {
            _snapshots[providerId] = next;
            if (persist) Persist();
        }
        Changed?.Invoke(next);
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_snapshots, JsonOptions));
            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Écriture de {Path} impossible", filePath);
        }
    }
}
