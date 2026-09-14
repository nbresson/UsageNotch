using System.Globalization;
using System.Text.Json;

namespace UsageNotch.Core.Usage;

public static class ClaudeUsageParser
{
    private static readonly (string Field, string Id, string[] Aliases)[] Fallbacks =
    [
        ("five_hour", "session", ["session", "five_hour"]),
        ("seven_day", "weekly_all", ["seven_day", "weekly_all", "weekly"]),
    ];

    /// <summary>Lit <c>limits[]</c>, puis fusionne <c>five_hour</c> / <c>seven_day</c> en secours. Lève <see cref="JsonException"/> si le JSON est illisible.</summary>
    public static IReadOnlyList<LimitWindow> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var list = new List<LimitWindow>();

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("limits", out var limits)
            && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in limits.EnumerateArray())
            {
                if (!TryString(l, "kind", out var kind)) continue;
                if (!TryDouble(l, "percent", out var pct)) continue;
                if (!TryReset(l, out var resets)) continue;
                list.Add(new LimitWindow(kind, LabelFor(kind), Fraction(pct), resets));
            }
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var (field, id, aliases) in Fallbacks)
            {
                MergeFallback(root, field, id, aliases, list);
            }
        }

        return list.OrderBy(w => w.Id == "session" ? 0 : 1).ToList();
    }

    public static string LabelFor(string kind) => kind switch
    {
        "session" => "Session en cours",
        "seven_day" or "weekly_all" => "Hebdomadaire (tous modèles)",
        "seven_day_opus" or "weekly_opus" => "Hebdomadaire (Opus)",
        "weekly_scoped" => "Hebdomadaire (par modèle)",
        _ => Humanize(kind),
    };

    private static void MergeFallback(JsonElement root, string field, string id, string[] aliases, List<LimitWindow> list)
    {
        if (!root.TryGetProperty(field, out var w) || w.ValueKind != JsonValueKind.Object) return;
        if (!TryDouble(w, "utilization", out var u)) return;
        if (!TryReset(w, out var resets)) return;

        var used = Fraction(u);
        var label = LabelFor(id);
        var duplicate = list.Any(x =>
            aliases.Contains(x.Id)
            || x.Label == label
            || (x.ResetsAt.ToUnixTimeSeconds() == resets.ToUnixTimeSeconds()
                && Math.Abs(x.UsedFraction - used) < 0.005));
        if (!duplicate)
        {
            list.Add(new LimitWindow(id, label, used, resets));
        }
    }

    private static double Fraction(double percent) => Math.Clamp(percent / 100.0, 0.0, 1.0);

    private static string Humanize(string kind)
    {
        var s = kind.Replace('_', ' ');
        return s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];
    }

    private static bool TryString(JsonElement e, string name, out string value)
    {
        value = "";
        if (e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            value = p.GetString() ?? "";
            return value.Length > 0;
        }
        return false;
    }

    private static bool TryDouble(JsonElement e, string name, out double value)
    {
        value = 0;
        return e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out value);
    }

    private static bool TryReset(JsonElement e, out DateTimeOffset value)
    {
        value = default;
        return e.TryGetProperty("resets_at", out var p)
            && p.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(p.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }
}
