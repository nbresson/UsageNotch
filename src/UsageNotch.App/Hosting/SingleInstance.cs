using System.Net.Http;
using UsageNotch.App.Interop;

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
        // Windows n'autorise un processus d'arrière-plan à passer au premier plan que si le processus au premier plan
        // (celui-ci, lancé par l'utilisateur) le lui permet : la fenêtre de réglages s'ouvre ainsi devant les autres.
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
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
