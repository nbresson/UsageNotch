using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Les alertes de seuil déjà envoyées, pour ne pas les répéter après un redémarrage. Chaque entrée expire à la
/// réinitialisation de sa fenêtre de limite ; les entrées expirées sont oubliées au chargement. Un fichier absent ou
/// illisible repart vide. Écriture atomique. À utiliser depuis un seul thread (le thread UI).
/// </summary>
public sealed class ThresholdLog(string filePath, TimeProvider time, ILogger<ThresholdLog> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly Dictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);

    public void Load()
    {
        _entries.Clear();
        try
        {
            if (!File.Exists(filePath)) return;
            var saved = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(filePath), JsonOptions);
            var now = time.GetUtcNow();
            foreach (var entry in saved ?? [])
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.ExpiresAt > now) _entries[entry.Key] = entry.ExpiresAt;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Lecture de {Path} impossible, alertes de seuil oubliées", filePath);
        }
    }

    public bool Contains(string key) => _entries.TryGetValue(key, out var expiresAt) && expiresAt > time.GetUtcNow();

    public void Add(string key, DateTimeOffset expiresAt)
    {
        _entries[key] = expiresAt;
        var now = time.GetUtcNow();
        foreach (var expired in _entries.Where(e => e.Value <= now).Select(e => e.Key).ToList()) _entries.Remove(expired);
        Write();
    }

    private void Write()
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = filePath + ".tmp";
            var entries = _entries.Select(e => new Entry(e.Key, e.Value)).OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
            File.WriteAllText(temp, JsonSerializer.Serialize(entries, JsonOptions));
            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Écriture de {Path} impossible", filePath);
        }
    }

    private sealed record Entry(string Key, DateTimeOffset ExpiresAt);
}
