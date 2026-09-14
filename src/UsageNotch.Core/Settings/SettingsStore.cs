using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Settings;

public sealed class SettingsStore(string filePath, ILogger<SettingsStore> logger, TimeProvider? time = null)
{
    private readonly TimeProvider time = time ?? TimeProvider.System;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageNotch");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public string FilePath { get; } = filePath;

    public Settings Current { get; private set; } = new();

    public event Action<Settings>? Changed;

    /// <summary>
    /// Lit le fichier (tolérant : clés absentes → défauts, clés inconnues ignorées, fichier corrompu → défauts) et le réécrit
    /// normalisé pour que le hook y trouve toujours le port. Un fichier indésérialisable est d'abord copié en
    /// <c>settings.json.corrupt-&lt;secondes unix&gt;</c> ; sans copie possible, il n'est pas écrasé.
    /// </summary>
    public Settings Load()
    {
        Settings loaded = new();
        var canWrite = true;
        try
        {
            if (File.Exists(FilePath))
            {
                loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? new Settings();
            }
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Réglages illisibles dans {Path}, valeurs par défaut", FilePath);
            canWrite = PreserveCorruptFile();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Réglages illisibles dans {Path}, valeurs par défaut", FilePath);
        }

        Current = loaded.Clamp();
        if (canWrite) Write(Current);
        return Current;
    }

    public void Save(Settings settings)
    {
        Current = settings.Clamp();
        Write(Current);
        Changed?.Invoke(Current);
    }

    /// <summary>Copie le fichier illisible sous le premier nom libre ; false si la copie a échoué (le fichier ne doit alors pas être écrasé).</summary>
    private bool PreserveCorruptFile()
    {
        var stem = $"{FilePath}.corrupt-{time.GetUtcNow().ToUnixTimeSeconds()}";
        try
        {
            for (var n = 0; n < 1000; n++)
            {
                var candidate = n == 0 ? stem : $"{stem}-{n}";
                if (File.Exists(candidate)) continue;
                try
                {
                    File.Copy(FilePath, candidate, overwrite: false);
                    logger.LogWarning("Réglages illisibles conservés dans {Path}", candidate);
                    return true;
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    // Nom pris entre-temps : essayer le suivant.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Copie des réglages illisibles impossible depuis {Path}", FilePath);
        }
        return false;
    }

    private void Write(Settings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Écriture des réglages impossible dans {Path}", FilePath);
        }
    }
}
