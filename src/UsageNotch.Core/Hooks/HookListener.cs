using System.Net;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Hooks;

/// <summary>
/// Écoute sur 127.0.0.1 uniquement. <c>POST /event?e=&lt;kind&gt;&amp;ppid=&lt;pid&gt;</c> reçoit le JSON du hook ;
/// <c>POST /open-settings</c> est le signal d'une seconde instance. HttpListener se lie à la boucle locale sans droits administrateur.
/// </summary>
public sealed class HookListener(int port, SessionStore sessions, ILogger<HookListener> logger) : BackgroundService
{
    public const int DefaultPort = 48666;
    public const int MaxBodyBytes = 256 * 1024;

    private readonly HttpListener _listener = new();

    public int Port { get; } = port;

    /// <summary>Vrai une fois le port lié avec succès ; faux avant, après l'arrêt ou si le port était indisponible.</summary>
    public bool IsListening { get; private set; }

    /// <summary>
    /// Levé sur un thread d'arrière-plan (celui de la requête), éventuellement dans le désordre : l'abonné doit
    /// basculer sur le thread UI avant d'agir.
    /// </summary>
    public event Action? OpenSettingsRequested;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException e)
        {
            IsListening = false;
            logger.LogError(e, "Port {Port} indisponible — une autre instance tourne peut-être", Port);
            return;
        }

        IsListening = true;
        try
        {
            await ListenAsync(stoppingToken);
        }
        finally
        {
            IsListening = false;
        }
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        using var stop = stoppingToken.Register(() => _listener.Stop());
        logger.LogInformation("Récepteur de hooks à l'écoute sur 127.0.0.1:{Port}", Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException e)
            {
                logger.LogWarning(e, "Requête entrante rejetée par HttpListener");
                continue;
            }
            _ = HandleSafelyAsync(context);
        }
    }

    private async Task HandleSafelyAsync(HttpListenerContext context)
    {
        try
        {
            await HandleAsync(context);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Traitement d'une requête de hook échoué");
            try { context.Response.Abort(); } catch (ObjectDisposedException) { }
        }
    }

    internal async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url?.AbsolutePath ?? "/";

        if (path is not ("/event" or "/open-settings"))
        {
            await RespondAsync(context.Response, 404, "not found");
            return;
        }

        if (!string.Equals(request.HttpMethod, "POST", StringComparison.Ordinal))
        {
            await RespondAsync(context.Response, 405, "method not allowed");
            return;
        }

        var headers = request.Headers.AllKeys
            .Where(k => k is not null)
            .SelectMany(k => (request.Headers.GetValues(k!) ?? []).Select(v => new KeyValuePair<string, string>(k!, v)));
        if (HookRequestGuard.IsForbidden(headers))
        {
            logger.LogWarning("Requête refusée sur {Path} (origine non locale)", path);
            await RespondAsync(context.Response, 403, "forbidden");
            return;
        }

        if (path == "/open-settings")
        {
            OpenSettingsRequested?.Invoke();
            await RespondAsync(context.Response, 200, "ok");
            return;
        }

        var kind = request.QueryString["e"] ?? "";
        if (!HookEvent.IsKnownKind(kind))
        {
            await RespondAsync(context.Response, 400, "unknown event");
            return;
        }

        var ppid = int.TryParse(request.QueryString["ppid"], out var parsed) ? parsed : 0;
        var body = await ReadBodyAsync(request);
        var ev = HookEventParser.Parse(kind, ppid, body);
        logger.LogDebug("Hook {Kind} pour la session {Session}", ev.Kind, ev.SessionId.Length > 8 ? ev.SessionId[..8] : ev.SessionId);
        sessions.Apply(ev);
        await RespondAsync(context.Response, 200, "ok");
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        long total = 0;
        int read;
        while ((read = await request.InputStream.ReadAsync(chunk)) > 0)
        {
            // On draine le flux jusqu'à sa fin même après avoir atteint MaxBodyBytes, pour ne répondre
            // qu'une fois la requête entièrement consommée ; seuls les octets jusqu'à la limite sont conservés.
            if (total < MaxBodyBytes)
            {
                var toKeep = (int)Math.Min(read, MaxBodyBytes - total);
                buffer.Write(chunk, 0, toKeep);
            }
            total += read;
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static async Task RespondAsync(HttpListenerResponse response, int status, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.StatusCode = status;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public override void Dispose()
    {
        IsListening = false;
        _listener.Close();
        base.Dispose();
    }
}
