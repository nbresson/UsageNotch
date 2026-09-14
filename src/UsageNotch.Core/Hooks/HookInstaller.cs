using System.Text.Json;
using System.Text.Json.Nodes;

namespace UsageNotch.Core.Hooks;

/// <summary>
/// Fusionne les sept hooks UsageNotch dans ~/.claude/settings.json sans toucher aux hooks de l'utilisateur.
/// Nos entrées sont reconnues par <see cref="Marker"/> dans la commande. Sauvegarde horodatée avant toute écriture.
/// </summary>
public sealed class HookInstaller(string settingsPath, string hookExePath, TimeProvider time)
{
    public const string Marker = "UsageNotch.Hook";

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
            return File.Exists(SettingsPath) && File.ReadAllText(SettingsPath).Contains(Marker, StringComparison.Ordinal);
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
                         && cmd.Contains(Marker, StringComparison.Ordinal));

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
            File.Copy(SettingsPath, $"{SettingsPath}.usagenotch-bak-{stamp}", overwrite: true);
        }

        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(WriteOptions));
        File.Move(temp, SettingsPath, overwrite: true);
    }
}
