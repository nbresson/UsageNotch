using System.Text.Json;

namespace UsageNotch.Core.Usage;

/// <summary>Analyse la réponse de l'API Costs d'OpenAI et produit les fenêtres de limites pour les 3 anneaux.</summary>
public static class OpenAiCostParser
{
    public static IReadOnlyList<LimitWindow> Parse(string json, double monthlyBudget, DateTimeOffset now)
    {
        if (monthlyBudget <= 0) monthlyBudget = 20.0;
        var dailyBudget = Math.Max(1.0, monthlyBudget / 30.0);
        var reasoningBudget = Math.Max(1.0, monthlyBudget * 0.5);

        var monthlyReset = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        var dailyReset = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);

        double totalMonthlyCost = 0.0;
        double todayCost = 0.0;
        double reasoningCost = 0.0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                var todayDate = now.UtcDateTime.Date;

                foreach (var item in dataEl.EnumerateArray())
                {
                    double itemCost = 0.0;
                    if (item.TryGetProperty("amount", out var amountEl))
                    {
                        if (amountEl.ValueKind == JsonValueKind.Number)
                        {
                            itemCost = amountEl.GetDouble();
                        }
                        else if (amountEl.ValueKind == JsonValueKind.Object &&
                                 amountEl.TryGetProperty("value", out var valEl) &&
                                 valEl.ValueKind == JsonValueKind.Number)
                        {
                            itemCost = valEl.GetDouble();
                        }
                    }

                    totalMonthlyCost += itemCost;

                    // Détection du jour courant via start_time ou timestamp
                    if (item.TryGetProperty("start_time", out var startEl) && startEl.ValueKind == JsonValueKind.Number)
                    {
                        var itemDate = DateTimeOffset.FromUnixTimeSeconds(startEl.GetInt64()).UtcDateTime.Date;
                        if (itemDate == todayDate)
                        {
                            todayCost += itemCost;
                        }
                    }
                    else if (item.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number)
                    {
                        var itemDate = DateTimeOffset.FromUnixTimeSeconds(tsEl.GetInt64()).UtcDateTime.Date;
                        if (itemDate == todayDate)
                        {
                            todayCost += itemCost;
                        }
                    }

                    // Détection des modèles de raisonnement (o1, o3...)
                    var lineItem = "";
                    if (item.TryGetProperty("line_item", out var lineEl) && lineEl.ValueKind == JsonValueKind.String)
                    {
                        lineItem = lineEl.GetString() ?? "";
                    }
                    else if (item.TryGetProperty("model", out var modelEl) && modelEl.ValueKind == JsonValueKind.String)
                    {
                        lineItem = modelEl.GetString() ?? "";
                    }

                    if (IsReasoningModel(lineItem))
                    {
                        reasoningCost += itemCost;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // En cas d'erreur de parsing partiel, on conserve ce qui a pu être extrait.
        }

        var monthlyFraction = Math.Clamp(totalMonthlyCost / monthlyBudget, 0.0, 1.0);
        var dailyFraction = Math.Clamp(todayCost / dailyBudget, 0.0, 1.0);
        var reasoningFraction = Math.Clamp(reasoningCost / reasoningBudget, 0.0, 1.0);

        return
        [
            new LimitWindow("monthly_cost", $"Budget mensuel ({Formatting.CurrencyFormatter.Format(totalMonthlyCost)} / {Formatting.CurrencyFormatter.Format(monthlyBudget, decimals: 0)})", monthlyFraction, monthlyReset),
            new LimitWindow("daily_cost", $"Consommation du jour ({Formatting.CurrencyFormatter.Format(todayCost)})", dailyFraction, dailyReset),
            new LimitWindow("reasoning_models", $"Modèles raisonnement o1/o3 ({Formatting.CurrencyFormatter.Format(reasoningCost)})", reasoningFraction, monthlyReset),
        ];
    }

    private static bool IsReasoningModel(string lineItem)
    {
        if (string.IsNullOrEmpty(lineItem)) return false;
        var lower = lineItem.ToLowerInvariant();
        return lower.Contains("o1") || lower.Contains("o3") || lower.Contains("reasoning");
    }
}
