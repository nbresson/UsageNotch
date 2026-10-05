using System.Text.Json;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Analyse la réponse de l'API wham/usage d'OpenAI (ChatGPT / Codex) et produit les fenêtres de limites pour les 3 anneaux :
/// - Session 5h (primary_window)
/// - Quota hebdomadaire (secondary_window)
/// - Modèles de raisonnement (additional_rate_limits)
/// </summary>
public static class OpenAiSubscriptionParser
{
    public static IReadOnlyList<LimitWindow> Parse(string json, DateTimeOffset now)
    {
        double primaryUsed = 0.0;
        DateTimeOffset primaryReset = now.AddHours(5);

        double secondaryUsed = 0.0;
        DateTimeOffset secondaryReset = now.AddDays(7);

        double extraUsed = 0.0;
        DateTimeOffset extraReset = primaryReset;
        string extraLabel = "Modèles raisonnement";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement rateLimitEl = default;
            if (root.TryGetProperty("rate_limit", out var rl) && rl.ValueKind == JsonValueKind.Object)
            {
                rateLimitEl = rl;
            }
            else if (root.TryGetProperty("rate_limits", out var rls) && rls.ValueKind == JsonValueKind.Object)
            {
                rateLimitEl = rls;
            }

            if (rateLimitEl.ValueKind == JsonValueKind.Object)
            {
                if (rateLimitEl.TryGetProperty("primary_window", out var pw) && pw.ValueKind == JsonValueKind.Object)
                {
                    primaryUsed = ExtractUsedFraction(pw);
                    if (ExtractResetTime(pw, now) is { } pr) primaryReset = pr;
                }

                if (rateLimitEl.TryGetProperty("secondary_window", out var sw) && sw.ValueKind == JsonValueKind.Object)
                {
                    secondaryUsed = ExtractUsedFraction(sw);
                    if (ExtractResetTime(sw, now) is { } sr) secondaryReset = sr;
                }
            }

            if (root.TryGetProperty("additional_rate_limits", out var addl) && addl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in addl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    string? title = null;
                    if (item.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String)
                    {
                        title = tEl.GetString();
                    }
                    else if (item.TryGetProperty("metering_type", out var mEl) && mEl.ValueKind == JsonValueKind.String)
                    {
                        title = mEl.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        extraLabel = $"Modèles raisonnement ({title})";
                    }

                    JsonElement itemWin = default;
                    if (item.TryGetProperty("rate_limit", out var itemRl) && itemRl.ValueKind == JsonValueKind.Object)
                    {
                        if (itemRl.TryGetProperty("primary_window", out var ipw)) itemWin = ipw;
                    }
                    else if (item.TryGetProperty("primary_window", out var ipwDirect))
                    {
                        itemWin = ipwDirect;
                    }

                    if (itemWin.ValueKind == JsonValueKind.Object)
                    {
                        extraUsed = ExtractUsedFraction(itemWin);
                        if (ExtractResetTime(itemWin, now) is { } er) extraReset = er;
                        break;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Tolérance aux formats partiels ou invalides
        }

        return
        [
            new LimitWindow("session", "Session 5h", Math.Clamp(primaryUsed, 0.0, 1.0), primaryReset),
            new LimitWindow("weekly", "Quota hebdomadaire", Math.Clamp(secondaryUsed, 0.0, 1.0), secondaryReset),
            new LimitWindow("reasoning_models", extraLabel, Math.Clamp(extraUsed, 0.0, 1.0), extraReset),
        ];
    }

    private static double ExtractUsedFraction(JsonElement element)
    {
        if (element.TryGetProperty("used_percent", out var upEl) && upEl.ValueKind == JsonValueKind.Number)
        {
            return upEl.GetDouble() / 100.0;
        }

        if (element.TryGetProperty("used_fraction", out var ufEl) && ufEl.ValueKind == JsonValueKind.Number)
        {
            return ufEl.GetDouble();
        }

        return 0.0;
    }

    private static DateTimeOffset? ExtractResetTime(JsonElement element, DateTimeOffset now)
    {
        if (element.TryGetProperty("reset_at", out var raEl) && raEl.ValueKind == JsonValueKind.Number)
        {
            var epoch = raEl.GetInt64();
            if (epoch > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(epoch);
            }
        }

        if (element.TryGetProperty("reset_after_seconds", out var rasEl) && rasEl.ValueKind == JsonValueKind.Number)
        {
            var seconds = rasEl.GetDouble();
            if (seconds >= 0)
            {
                return now.AddSeconds(seconds);
            }
        }

        return null;
    }
}
