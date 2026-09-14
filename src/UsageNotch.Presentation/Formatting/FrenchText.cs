using System.Globalization;

namespace UsageNotch.Presentation.Formatting;

/// <summary>Tous les textes chiffrés affichés par le notch, en français.</summary>
public static class FrenchText
{
    public const char Nbsp = '\u00A0';

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Percent(double fraction)
    {
        var value = (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * 100, MidpointRounding.AwayFromZero);
        return $"{value}{Nbsp}%";
    }

    /// <summary>Sous une heure : relatif. Sous 24 h : heure. Au-delà : jour abrégé et heure.</summary>
    public static string ResetCopy(DateTimeOffset resetsAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        var diff = resetsAt - now;
        if (diff <= TimeSpan.Zero) return "Réinitialisation imminente";
        if (diff < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)Math.Round(diff.TotalMinutes, MidpointRounding.AwayFromZero));
            return $"Réinitialisation dans {minutes} min";
        }

        var local = TimeZoneInfo.ConvertTime(resetsAt, zone);
        var time = local.ToString("HH:mm", French);
        if (diff < TimeSpan.FromHours(24)) return $"Réinitialisation à {time}";
        return $"Réinitialisation {local.ToString("ddd", French)} {time}";
    }

    public static string UpdatedAgo(DateTimeOffset fetchedAt, DateTimeOffset now)
    {
        var age = now - fetchedAt;
        if (age < TimeSpan.FromMinutes(1)) return "Mis à jour à l'instant";
        if (age < TimeSpan.FromHours(1)) return $"Mis à jour il y a {(int)age.TotalMinutes} min";
        if (age < TimeSpan.FromHours(48)) return $"Mis à jour il y a {(int)age.TotalHours} h";
        return $"Mis à jour il y a {(int)age.TotalDays} j";
    }

    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span < TimeSpan.FromMinutes(1)) return $"{(int)span.TotalSeconds} s";
        if (span < TimeSpan.FromHours(1)) return $"{(int)span.TotalMinutes} min";
        return $"{(int)span.TotalHours} h {span.Minutes:00}";
    }
}
