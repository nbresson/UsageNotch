using System.Text.Json;
using System.Text.Json.Nodes;

namespace UsageNotch.Core.Hooks;

/// <summary>
/// Fusionne les sept hooks UsageNotch dans ~/.claude/settings.json sans toucher aux hooks de l'utilisateur.
/// Nos entrées sont reconnues par le nom de fichier de l'exécutable dans la commande (voir
/// <see cref="IsOurCommand"/>), jamais par une simple recherche de sous-chaîne. Sauvegarde horodatée
/// (jamais écrasée) avant toute écriture.
/// </summary>
public sealed class HookInstaller(string settingsPath, string hookExePath, TimeProvider time)
{
    /// <summary>
    /// Conservé pour compatibilité (nom historique de nos entrées) mais n'est plus utilisé pour
    /// reconnaître nos hooks : voir <see cref="IsOurCommand"/>, qui compare le nom de fichier de
    /// l'exécutable plutôt que de chercher cette sous-chaîne dans la commande.
    /// </summary>
    public const string Marker = "UsageNotch.Hook";

    private const string HookExeFileName = "UsageNotch.Hook.exe";

    public static readonly (string Event, bool NeedsMatcher, string Argument)[] Wiring =
    [
        ("SessionStart", false, "session_start"),
        ("UserPromptSubmit", false, "running"),
        ("PreToolUse", true, "running"),
        ("PostToolUse", true, "running"),
        ("Notification", false, "attention"),
        ("Stop", false, "done"),
        ("SessionEnd", false, "session_end"),
    ];

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string DefaultSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public string SettingsPath { get; } = settingsPath;
    public string HookExePath { get; } = hookExePath;

    public bool IsInstalled()
    {
        try
        {
            var root = LoadObject();
            return root["hooks"] is JsonObject hooks
                && hooks.Any(kv => kv.Value is JsonArray array && array.Any(entry => entry is not null && IsOurs(entry)));
        }
        catch (IOException)
        {
            return false;
        }
    }

    public string Install()
    {
        if (!File.Exists(HookExePath))
        {
            throw new FileNotFoundException("Exécutable hook introuvable", HookExePath);
        }

        var root = LoadObject();
        if (root["hooks"] is not JsonObject hooks)
        {
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }

        foreach (var (evt, needsMatcher, argument) in Wiring)
        {
            var existing = hooks[evt] as JsonArray ?? new JsonArray();
            var kept = new JsonArray();
            foreach (var entry in existing.ToList())
            {
                if (entry is not null && !IsOurs(entry))
                {
                    existing.Remove(entry);
                    kept.Add(entry);
                }
            }
            var ours = new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = $"\"{HookExePath}\" {argument}",
                    ["timeout"] = 5,
                }),
            };
            if (needsMatcher) ours["matcher"] = "*";
            kept.Add(ours);
            hooks[evt] = kept;
        }

        BackupAndWrite(root);
        return $"{Wiring.Length} hooks écrits dans {SettingsPath}";
    }

    public string Uninstall()
    {
        if (!File.Exists(SettingsPath)) return "settings.json absent, rien à retirer";

        var root = LoadObject();
        if (root["hooks"] is not JsonObject hooks) return "aucun hook configuré, rien à retirer";

        var removed = 0;
        foreach (var key in hooks.Select(kv => kv.Key).ToList())
        {
            if (hooks[key] is not JsonArray array) continue;
            var kept = new JsonArray();
            foreach (var entry in array.ToList())
            {
                array.Remove(entry);
                if (entry is not null && IsOurs(entry)) removed++;
                else if (entry is not null) kept.Add(entry);
            }
            if (kept.Count == 0) hooks.Remove(key);
            else hooks[key] = kept;
        }

        BackupAndWrite(root);
        return $"{removed} hook(s) UsageNotch retiré(s)";
    }

    private static bool IsOurs(JsonNode entry) =>
        entry is JsonObject obj
        && obj["hooks"] is JsonArray list
        && list.Any(h => h is JsonObject ho
                         && ho["command"] is JsonValue v
                         && v.TryGetValue<string>(out var cmd)
                         && IsOurCommand(cmd));

    /// <summary>
    /// Vrai si <paramref name="command"/> lance notre exécutable, identifié par son nom de fichier
    /// (<c>UsageNotch.Hook.exe</c>), jamais par une correspondance de sous-chaîne : un hook tiers dont la
    /// commande mentionne simplement notre nom ("echo UsageNotch.Hook est génial") n'est pas le nôtre, et
    /// une installation antérieure dans un autre dossier reste reconnue comme la nôtre.
    /// </summary>
    internal static bool IsOurCommand(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0) return false;

        string token;
        if (trimmed[0] == '"')
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote < 0) return false;
            token = trimmed[1..closingQuote];
        }
        else
        {
            var whitespace = trimmed.IndexOfAny([' ', '\t']);
            token = whitespace < 0 ? trimmed : trimmed[..whitespace];
        }

        try
        {
            return string.Equals(Path.GetFileName(token), HookExeFileName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private JsonObject LoadObject()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new JsonObject();
            return JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private void BackupAndWrite(JsonObject root)
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(SettingsPath))
        {
            var stamp = time.GetUtcNow().ToUnixTimeSeconds();
            var backupPath = $"{SettingsPath}.usagenotch-bak-{stamp}";
            var suffix = 1;
            while (File.Exists(backupPath))
            {
                backupPath = $"{SettingsPath}.usagenotch-bak-{stamp}-{suffix}";
                suffix++;
            }
            File.Copy(SettingsPath, backupPath, overwrite: false);
        }

        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(WriteOptions));
        File.Move(temp, SettingsPath, overwrite: true);
    }
}
