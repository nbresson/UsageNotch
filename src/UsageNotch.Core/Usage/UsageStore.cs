using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Compose le snapshot publié à partir des résultats d'appel : conserve la dernière bonne lecture,
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

    public UsageSnapshot Current { get; private set; } = UsageSnapshot.Empty;

    public event Action<UsageSnapshot>? Changed;

    public bool IsInBackoff => Current.BackoffUntil is { } until && until > time.GetUtcNow();

    /// <summary>Recharge la lecture persistée. Une vieille valeur vaut mieux qu'un anneau vide : elle est publiée en Stale.</summary>
    public void Load()
    {
        UsageSnapshot? saved;
        try
        {
            if (!File.Exists(filePath)) return;
            saved = JsonSerializer.Deserialize<UsageSnapshot>(File.ReadAllText(filePath), JsonOptions);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Lecture de {Path} impossible, on repart de zéro", filePath);
            return;
        }
        if (saved is null) return;

        var restored = saved.Windows.Count > 0
            ? saved with { Status = SnapshotStatus.Stale }
            : saved with { Status = saved.Status == SnapshotStatus.Ok ? SnapshotStatus.Error : saved.Status };
        Publish(restored, persist: false);
    }

    public void Apply(FetchResult result, TimeSpan backoffWait = default)
    {
        var now = time.GetUtcNow();
        var prev = Current;
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

        Publish(next, persist: true);
    }

    /// <summary>« Rafraîchir maintenant » : l'échéance est oubliée pour que le prochain cycle appelle tout de suite.</summary>
    public void ClearBackoff()
    {
        if (Current.BackoffUntil is null) return;
        var next = Current with
        {
            BackoffUntil = null,
            Status = Current.Windows.Count > 0 ? SnapshotStatus.Stale : SnapshotStatus.Error,
        };
        Publish(next, persist: true);
    }

    private void Publish(UsageSnapshot next, bool persist)
    {
        lock (_gate)
        {
            Current = next;
            if (persist) Persist(next);
        }
        Changed?.Invoke(next);
    }

    private void Persist(UsageSnapshot snapshot)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Écriture de {Path} impossible", filePath);
        }
    }
}
