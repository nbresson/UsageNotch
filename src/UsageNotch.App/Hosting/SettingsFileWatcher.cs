using System.IO;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>
/// Applique à chaud les modifications faites à la main dans settings.json. Le fichier est lu et analysé hors du thread
/// UI ; seul l'enregistrement du résultat passe par le thread UI. Nos propres écritures (fenêtre de réglages, glisser
/// Alt, Quitter) sont reconnues parce que le fichier relu donne exactement les réglages courants : aucune relecture
/// n'est faite après un enregistrement.
/// </summary>
public sealed class SettingsFileWatcher(SettingsStore store, AppPaths paths, IUiDispatcher ui, ILogger<SettingsFileWatcher> logger) : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);
    private const int ReadAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly object _readLock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _lastRejectedText;
    private volatile bool _disposed;

    public void Start()
    {
        _watcher = new FileSystemWatcher(paths.DataDirectory, Path.GetFileName(paths.SettingsFile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.Error += OnWatcherError;
        _debounce = new Timer(_ => ReadInBackground(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        try
        {
            _debounce?.Change(Debounce, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e) =>
        logger.LogWarning(e.GetException(), "Surveillance de {Path} interrompue : les modifications manuelles ne seront plus relues", paths.SettingsFile);

    /// <summary>Sur un thread du pool : lecture avec quelques essais courts, analyse, puis remise au thread UI.</summary>
    private void ReadInBackground()
    {
        if (_disposed) return;

        string? text;
        lock (_readLock)
        {
            text = ReadText();
        }
        if (text is null) return;

        if (SettingsStore.TryParse(text, out var parsed))
        {
            ui.Post(() => Apply(parsed));
        }
        else
        {
            ui.Post(() => ReportUnreadable(text));
        }
    }

    /// <summary>Thread UI. Un fichier identique aux réglages courants (nos propres écritures) ne déclenche rien.</summary>
    private void Apply(Settings parsed)
    {
        if (_disposed) return;
        _lastRejectedText = null;
        if (parsed == store.Current) return;

        var previousPort = store.Current.Port;
        store.Save(parsed);
        logger.LogInformation("Réglages relus depuis {Path}", paths.SettingsFile);
        if (parsed.Port != previousPort)
        {
            logger.LogWarning("Le port passe de {Old} à {New} : redémarrez UsageNotch pour l'appliquer", previousPort, parsed.Port);
        }
    }

    /// <summary>
    /// Thread UI. N'écrase jamais settings.json : un texte illisible (JSON malformé, énumération inconnue) est ignoré et
    /// journalisé une fois, le fichier reste tel quel pour que l'utilisateur puisse le corriger.
    /// </summary>
    private void ReportUnreadable(string text)
    {
        if (_disposed || text == _lastRejectedText) return;
        _lastRejectedText = text;
        logger.LogWarning("settings.json illisible, modification ignorée : {Path}", paths.SettingsFile);
    }

    /// <summary>L'éditeur peut tenir le fichier ouvert un instant : quelques essais courts, hors du thread UI.</summary>
    private string? ReadText()
    {
        for (var attempt = 0; attempt < ReadAttempts; attempt++)
        {
            try
            {
                return File.Exists(paths.SettingsFile) ? File.ReadAllText(paths.SettingsFile) : null;
            }
            catch (IOException)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(RetryDelay);
            }
        }
        return null;
    }

    public void Dispose()
    {
        _disposed = true;
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
