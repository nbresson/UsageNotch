using UsageNotch.Core.Placement;
using UsageNotch.Presentation.Formatting;

namespace UsageNotch.Presentation.Preferences;

/// <summary>La liste « écran d'ancrage » : « Écran principal » puis chaque écran numéroté de gauche à droite.</summary>
public static class MonitorChoices
{
    /// <summary>Clé de « Écran principal » : une liste déroulante WPF ne sait pas sélectionner une valeur null.</summary>
    public const string PrimaryKey = "";

    /// <summary>Ordre d'affichage stable : de gauche à droite, puis de haut en bas. Il sert à numéroter les écrans.</summary>
    public static IReadOnlyList<MonitorInfo> Ordered(IReadOnlyList<MonitorInfo> monitors) =>
        monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();

    public static string Describe(MonitorInfo monitor, int number)
    {
        var percent = (int)Math.Round(monitor.Scale * 100, MidpointRounding.AwayFromZero);
        var label = $"Écran {number} — {monitor.Bounds.Width} × {monitor.Bounds.Height}, {percent}{FrenchText.Nbsp}%";
        return monitor.IsPrimary ? label + " (principal)" : label;
    }

    /// <summary>Un écran mémorisé mais absent reste dans la liste : son choix n'est jamais perdu (spec §8).</summary>
    public static IReadOnlyList<Choice<string>> Build(IReadOnlyList<MonitorInfo> monitors, string? selectedDeviceId)
    {
        var ordered = Ordered(monitors);
        var choices = new List<Choice<string>> { new(PrimaryKey, "Écran principal") };
        for (var i = 0; i < ordered.Count; i++)
        {
            choices.Add(new Choice<string>(ordered[i].DeviceId, Describe(ordered[i], i + 1)));
        }

        if (selectedDeviceId is not null
            && !ordered.Any(m => m.DeviceId.Equals(selectedDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new Choice<string>(selectedDeviceId, $"Écran absent ({selectedDeviceId}) — pilule sur l'écran principal"));
        }
        return choices;
    }

    /// <summary>La clé à sélectionner pour un identifiant mémorisé, sans tenir compte de la casse.</summary>
    public static string KeyFor(IReadOnlyList<Choice<string>> choices, string? deviceId)
    {
        if (deviceId is null) return PrimaryKey;
        return choices.FirstOrDefault(c => c.Value.Length > 0 && c.Value.Equals(deviceId, StringComparison.OrdinalIgnoreCase))?.Value
            ?? deviceId;
    }

    public static string? DeviceIdFor(string? key) => string.IsNullOrEmpty(key) ? null : key;
}
