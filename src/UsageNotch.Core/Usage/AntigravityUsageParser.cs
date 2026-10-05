using System.Globalization;
using System.Text.Json;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Analyse la réponse JSON ConnectRPC de <c>RetrieveUserQuotaSummary</c> émise par le hub Antigravity.
/// </summary>
public static class AntigravityUsageParser
{
    public static IReadOnlyList<LimitWindow> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var list = new List<LimitWindow>();

        if (root.ValueKind != JsonValueKind.Object)
        {
            return list;
        }

        var target = root;
        if (root.TryGetProperty("response", out var resp) && resp.ValueKind == JsonValueKind.Object)
        {
            target = resp;
        }

        if (target.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var bucket in buckets.EnumerateArray())
                {
                    if (!TryString(bucket, "bucketId", out var bucketId) || bucketId.Length == 0)
                    {
                        continue;
                    }

                    if (!TryDouble(bucket, "remainingFraction", out var remaining))
                    {
                        continue;
                    }

                    if (!TryReset(bucket, out var resetsAt))
                    {
                        continue;
                    }

                    TryString(bucket, "displayName", out var displayName);
                    var label = LabelFor(bucketId, displayName);
                    var usedFraction = Math.Clamp(1.0 - remaining, 0.0, 1.0);

                    list.Add(new LimitWindow(bucketId, label, usedFraction, resetsAt));
                }
            }
        }

        return list.OrderBy(SortOrder).ToList();
    }

    public static string LabelFor(string bucketId, string? displayName = null) => bucketId switch
    {
        "gemini-5h" => "Modèles Gemini (5 h)",
        "gemini-weekly" => "Modèles Gemini (hebdomadaire)",
        "3p-weekly" => "Modèles tiers (hebdomadaire)",
        "3p-5h" => "Modèles tiers (5 h)",
        _ when !string.IsNullOrWhiteSpace(displayName) => displayName,
        _ => Humanize(bucketId),
    };

    private static int SortOrder(LimitWindow w) => w.Id switch
    {
        "gemini-5h" or "5h" => 0,
        "gemini-weekly" or "weekly" => 1,
        "3p-weekly" or "3p-5h" => 2,
        _ => 3,
    };

    private static string Humanize(string id)
    {
        var s = id.Replace('-', ' ').Replace('_', ' ');
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
        return e.TryGetProperty("resetTime", out var p)
            && p.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(p.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }
}
