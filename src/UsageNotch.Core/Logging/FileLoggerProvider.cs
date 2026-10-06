using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Logging;

/// <summary>
/// Un fichier par jour UTC dans <paramref name="directory"/>, conservé 7 jours. Le niveau minimum est relu à chaque appel,
/// ce qui permet de basculer le mode Debug depuis les réglages sans redémarrer. N'écrit jamais d'exception vers l'appelant.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const int RetentionDays = 14;
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxRotatedFilesPerDay = 5;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly Func<LogLevel> _minimumLevel;
    private readonly int _retentionDays;
    private readonly long _maxFileSizeBytes;
    private readonly object _gate = new();
    private DateTime _purgedDay;

    public FileLoggerProvider(
        string directory,
        TimeProvider time,
        Func<LogLevel> minimumLevel,
        int retentionDays = RetentionDays,
        long maxFileSizeBytes = MaxFileSizeBytes)
    {
        _directory = directory;
        _time = time;
        _minimumLevel = minimumLevel;
        _retentionDays = retentionDays;
        _maxFileSizeBytes = maxFileSizeBytes;
        try
        {
            Directory.CreateDirectory(directory);
            _purgedDay = time.GetUtcNow().UtcDateTime.Date;
            Purge();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Journal indisponible : l'application continue sans.
        }
    }

    public static string FileNameFor(DateTimeOffset utc) =>
        "usagenotch-" + utc.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= _minimumLevel();

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _time.GetUtcNow();
        var line = new StringBuilder()
            .Append(now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .Append(" [").Append(Abbreviation(level)).Append("] ")
            .Append(category).Append(": ").Append(message);
        if (exception is not null) line.Append(Environment.NewLine).Append(exception);
        line.Append(Environment.NewLine);

        try
        {
            lock (_gate)
            {
                // L'application peut tourner plusieurs jours : la rétention s'applique aussi au changement de jour.
                if (now.UtcDateTime.Date != _purgedDay)
                {
                    _purgedDay = now.UtcDateTime.Date;
                    Purge();
                }
                var mainFilePath = Path.Combine(_directory, FileNameFor(now));
                var targetFile = GetTargetLogFile(mainFilePath);
                File.AppendAllText(targetFile, line.ToString(), Utf8NoBom);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Voir la remarque du constructeur.
        }
    }

    private string GetTargetLogFile(string mainFilePath)
    {
        if (!File.Exists(mainFilePath)) return mainFilePath;
        var info = new FileInfo(mainFilePath);
        if (info.Length < _maxFileSizeBytes) return mainFilePath;

        var baseWithoutExt = Path.Combine(Path.GetDirectoryName(mainFilePath)!, Path.GetFileNameWithoutExtension(mainFilePath));
        for (var i = 1; i <= MaxRotatedFilesPerDay; i++)
        {
            var rotated = $"{baseWithoutExt}.{i}.log";
            if (!File.Exists(rotated) || new FileInfo(rotated).Length < _maxFileSizeBytes)
            {
                return rotated;
            }
        }
        return $"{baseWithoutExt}.{MaxRotatedFilesPerDay}.log";
    }

    private void Purge()
    {
        var cutoff = _time.GetUtcNow().UtcDateTime.Date.AddDays(-_retentionDays);
        foreach (var path in Directory.EnumerateFiles(_directory, "usagenotch-*.log"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("usagenotch-", StringComparison.Ordinal)) continue;
            var parts = name["usagenotch-".Length..].Split('.');
            var stamp = parts[0];
            if (!DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
            if (day >= cutoff) continue;
            try { File.Delete(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string Abbreviation(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(logLevel, category, formatter(state, exception), exception);
        }
    }
}
