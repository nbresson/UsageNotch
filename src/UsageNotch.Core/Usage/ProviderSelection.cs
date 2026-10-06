namespace UsageNotch.Core.Usage;

/// <summary>Identifiants constants des fournisseurs d'usage supportés.</summary>
public static class ProviderIds
{
    public const string Claude = "claude";
    public const string Antigravity = "antigravity";
    public const string OpenAi = "openai";

    public static readonly IReadOnlyList<string> All = [Claude, Antigravity, OpenAi];
}

/// <summary>
/// Résolution et normalisation de la sélection de fournisseurs.
/// Élimine l'explosion combinatoire des chaînes de réglages tout en conservant
/// une rétrocompatibilité intégrale avec les configurations existantes.
/// </summary>
public static class ProviderSelection
{
    private static readonly char[] Delimiters = [',', '+', ';', '|', ' '];

    /// <summary>
    /// Résout la liste ordonnée des identifiants de fournisseurs actifs depuis une valeur de réglage.
    /// Accepte les alias historiques (« both », « all », « claude_openai », etc.) ainsi que les listes délimitées.
    /// </summary>
    public static IReadOnlyList<string> Resolve(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting))
        {
            return [ProviderIds.Claude, ProviderIds.Antigravity];
        }

        var normalized = setting.Trim().ToLowerInvariant();

        var list = normalized switch
        {
            "all" => [ProviderIds.Claude, ProviderIds.Antigravity, ProviderIds.OpenAi],
            "both" => [ProviderIds.Claude, ProviderIds.Antigravity],
            "claude_openai" => [ProviderIds.Claude, ProviderIds.OpenAi],
            "antigravity_openai" => [ProviderIds.Antigravity, ProviderIds.OpenAi],
            "claude" => [ProviderIds.Claude],
            "antigravity" => [ProviderIds.Antigravity],
            "openai" => [ProviderIds.OpenAi],
            _ => ParseCustomList(normalized)
        };

        return list.Count > 0 ? list : [ProviderIds.Claude];
    }

    /// <summary>
    /// Vérifie si une chaîne de réglage correspond à au moins un fournisseur valide.
    /// </summary>
    public static bool IsValid(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting)) return false;
        var normalized = setting.Trim().ToLowerInvariant();

        return normalized switch
        {
            "all" or "both" or "claude_openai" or "antigravity_openai" or "claude" or "antigravity" or "openai" => true,
            _ => ParseCustomList(normalized).Count > 0
        };
    }

    private static IReadOnlyList<string> ParseCustomList(string raw)
    {
        var parts = raw.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<string>();

        foreach (var part in parts)
        {
            var match = part switch
            {
                "claude" or "anthropic" => ProviderIds.Claude,
                "antigravity" or "gemini" or "google" => ProviderIds.Antigravity,
                "openai" or "chatgpt" or "codex" => ProviderIds.OpenAi,
                _ => null
            };

            if (match is not null && !result.Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(match);
            }
        }

        return result;
    }
}
