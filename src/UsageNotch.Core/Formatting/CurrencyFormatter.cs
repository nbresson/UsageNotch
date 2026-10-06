using System.Globalization;

namespace UsageNotch.Core.Formatting;

/// <summary>
/// Service utilitaire de formatage monétaire uniforme pour tous les modèles et vues.
/// </summary>
public static class CurrencyFormatter
{
    public const char Nbsp = '\u00A0';

    /// <summary>
    /// Formate un montant en devise (ex. « 12.50 $ », « 20 $ »).
    /// </summary>
    public static string Format(double amount, string symbol = "$", int decimals = 2)
    {
        var formatted = decimals == 0
            ? amount.ToString("F0", CultureInfo.InvariantCulture)
            : amount.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        return $"{formatted} {symbol}";
    }
}
