namespace UsageNotch.Hook;

/// <summary>Lecture textuelle minimale de <c>"port": N</c> dans settings.json : pas de parseur JSON dans le hook, pour rester minuscule et rapide.</summary>
public static class PortReader
{
    public const int DefaultPort = 48666;

    public static string DefaultSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageNotch", "settings.json");

    public static int Read(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson)) return DefaultPort;

        var key = settingsJson.IndexOf("\"port\"", StringComparison.Ordinal);
        if (key < 0) return DefaultPort;

        var i = key + "\"port\"".Length;
        while (i < settingsJson.Length && (settingsJson[i] == ' ' || settingsJson[i] == ':' || settingsJson[i] == '\t')) i++;

        var start = i;
        while (i < settingsJson.Length && char.IsAsciiDigit(settingsJson[i])) i++;
        if (i == start) return DefaultPort;

        return int.TryParse(settingsJson.AsSpan(start, i - start), out var port) && port is >= 1024 and <= 65535
            ? port
            : DefaultPort;
    }
}
