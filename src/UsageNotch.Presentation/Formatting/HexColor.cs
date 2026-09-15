using System.Globalization;

namespace UsageNotch.Presentation.Formatting;

/// <summary>Couleurs saisies par l'utilisateur. Forme normalisée : <c>#RRGGBB</c> en majuscules.</summary>
public static class HexColor
{
    /// <summary>Accepte « #RRGGBB », « RRGGBB », « #RGB » ou « RGB », espaces autour ignorés, casse indifférente.</summary>
    public static bool TryNormalize(string? input, out string hex)
    {
        hex = "";
        if (input is null) return false;

        var digits = input.Trim();
        if (digits.StartsWith('#')) digits = digits[1..];
        if (digits.Length == 3) digits = string.Concat(digits.Select(c => new string(c, 2)));
        if (digits.Length != 6 || !digits.All(char.IsAsciiHexDigit)) return false;

        hex = "#" + digits.ToUpperInvariant();
        return true;
    }

    /// <summary>COLORREF Win32 : <c>0x00BBGGRR</c>.</summary>
    /// <exception cref="ArgumentException">La couleur n'est pas lisible par <see cref="TryNormalize"/>.</exception>
    public static uint ToColorRef(string hex)
    {
        if (!TryNormalize(hex, out var normalized))
        {
            throw new ArgumentException($"Couleur invalide : {hex}", nameof(hex));
        }

        var r = uint.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = uint.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = uint.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return r | (g << 8) | (b << 16);
    }

    public static string FromColorRef(uint colorRef) =>
        $"#{colorRef & 0xFF:X2}{(colorRef >> 8) & 0xFF:X2}{(colorRef >> 16) & 0xFF:X2}";
}
