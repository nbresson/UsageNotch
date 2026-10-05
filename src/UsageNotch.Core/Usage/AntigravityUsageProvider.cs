using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Interroge l'endpoint local ConnectRPC de Google Antigravity (<c>RetrieveUserQuotaSummary</c>)
/// avec le jeton CSRF et le port découverts sur le processus <c>agy.exe</c>.
/// </summary>
public sealed class AntigravityUsageProvider(
    HttpClient http,
    AntigravityProcessDiscovery discovery,
    ILogger<AntigravityUsageProvider> logger) : IUsageProvider
{
    public const string EndpointPath = "/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public string Id => "antigravity";
    public string DisplayName => "Google Antigravity";
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.Antigravity;

    public async Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var hub = discovery.Discover();
        if (hub is null)
        {
            return new FetchResult.NeedsAuth("Antigravity n'est pas ouvert — lancez Antigravity pour lire l'usage.");
        }

        var result = await FetchOnceAsync(hub, ct);
        if (result is FetchResult.NeedsAuth)
        {
            // Le hub a peut-être redémarré (nouveau token ou nouveau port).
            discovery.InvalidateCache();
            var fresh = discovery.Discover();
            if (fresh is not null && (fresh.Port != hub.Port || fresh.CsrfToken != hub.CsrfToken))
            {
                logger.LogInformation("Hub Antigravity renouvelé après refus, nouvel essai");
                result = await FetchOnceAsync(fresh, ct);
            }
        }

        return result;
    }

    private async Task<FetchResult> FetchOnceAsync(AntigravityHubInfo hub, CancellationToken ct)
    {
        try
        {
            var uri = new Uri($"http://127.0.0.1:{hub.Port}{EndpointPath}");
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.TryAddWithoutValidation("x-codeium-csrf-token", hub.CsrfToken);
            request.Content = new StringContent("{\"forceRefresh\": true}", System.Text.Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, ct);
            var code = (int)response.StatusCode;

            switch (code)
            {
                case 200:
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var windows = AntigravityUsageParser.Parse(json);
                    if (windows.Count == 0)
                    {
                        logger.LogWarning("Usage Antigravity : réponse 200 sans quota reconnu");
                        return new FetchResult.Failed("Réponse sans quota reconnu");
                    }
                    return new FetchResult.Success(windows);

                case 401:
                case 403:
                    discovery.InvalidateCache();
                    return new FetchResult.NeedsAuth("Session Antigravity refusée (redémarrage en cours ?).");

                default:
                    logger.LogWarning("Usage Antigravity : HTTP {Code}", code);
                    return new FetchResult.Failed($"HTTP {code}");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FetchResult.Failed($"Délai dépassé ({(int)http.Timeout.TotalSeconds} s)");
        }
        catch (HttpRequestException e)
        {
            discovery.InvalidateCache();
            logger.LogWarning(e, "Usage Antigravity : erreur de connexion locale");
            return new FetchResult.Failed("Connexion impossible au hub Antigravity");
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Usage Antigravity : réponse JSON illisible");
            return new FetchResult.Failed("Réponse illisible");
        }
    }
}
