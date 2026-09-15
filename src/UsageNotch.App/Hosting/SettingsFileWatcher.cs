using System.IO;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>Applique à chaud les modifications faites à la main dans settings.json.</summary>
public sealed class SettingsFileWatcher(SettingsStore store, AppPaths paths, IUiDispatcher ui, ILogger<SettingsFileWatcher> logger) : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _lastText;
    private Action<Settings>? _onSaved;

    public void Start()
    {
        _lastText = ReadText();
        _onSaved = _ => _lastText = ReadText();
        store.Changed += _onSaved;

        _watcher = new FileSystemWatcher(paths.DataDirectory, Path.GetFileName(paths.SettingsFile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
        _debounce = new Timer(_ => ui.Post(Reload), null, Timeout.Infinite, Timeout.Infinite);
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) =>
        _debounce?.Change(Debounce, Timeout.InfiniteTimeSpan);

    private void Reload()
    {
        var text = ReadText();
        if (text is null || text == _lastText) return;

        var previousPort = store.Current.Port;
        var loaded = store.Load();
        store.Save(loaded);
        logger.LogInformation("Réglages relus depuis {Path}", paths.SettingsFile);
        if (loaded.Port != previousPort)
        {
            logger.LogWarning("Le port passe de {Old} à {New} : redémarrez UsageNotch pour l'appliquer", previousPort, loaded.Port);
        }
    }

    /// <summary>L'éditeur peut tenir le fichier ouvert un instant : quelques essais courts.</summary>
    private string? ReadText()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return File.Exists(paths.SettingsFile) ? File.ReadAllText(paths.SettingsFile) : null;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (_onSaved is not null) store.Changed -= _onSaved;
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
