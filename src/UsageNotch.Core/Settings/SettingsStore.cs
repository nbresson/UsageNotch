using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Settings;

public sealed class SettingsStore(string filePath, ILogger<SettingsStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageNotch");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public string FilePath { get; } = filePath;

    public Settings Current { get; private set; } = new();

    public event Action<Settings>? Changed;

    /// <summary>Lit le fichier (tolérant : clés absentes → défauts, clés inconnues ignorées, fichier corrompu → défauts) et le réécrit normalisé pour que le hook y trouve toujours le port.</summary>
    public Settings Load()
    {
        Settings loaded = new();
        try
        {
            if (File.Exists(FilePath))
            {
                loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? new Settings();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Réglages illisibles dans {Path}, valeurs par défaut", FilePath);
        }

        Current = loaded.Clamp();
        Write(Current);
        return Current;
    }

    public void Save(Settings settings)
    {
        Current = settings.Clamp();
        Write(Current);
        Changed?.Invoke(Current);
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
