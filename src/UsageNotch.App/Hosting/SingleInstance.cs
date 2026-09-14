using System.Net.Http;

namespace UsageNotch.App.Hosting;

/// <summary>Mutex nommé de session. Libéré sur le thread qui l'a acquis (le thread UI).</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    public SingleInstance(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirst = createdNew;
    }

    public bool IsFirst { get; }

    /// <summary>Demande à l'instance existante de se montrer. Échec silencieux : l'autre instance peut être en train de démarrer.</summary>
    public static async Task SignalExistingAsync(int port)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.PostAsync($"http://127.0.0.1:{port}/open-settings", content: null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
