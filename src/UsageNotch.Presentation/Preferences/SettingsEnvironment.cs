namespace UsageNotch.Presentation.Preferences;

/// <summary>Ce que la fenêtre de réglages affiche sans pouvoir le modifier. <paramref name="ListeningPort"/> : port réellement écouté.</summary>
public sealed record SettingsEnvironment(
    string Version,
    bool Demo,
    string DataDirectory,
    string LogsDirectory,
    string SettingsFile,
    int ListeningPort,
    bool Listening);
