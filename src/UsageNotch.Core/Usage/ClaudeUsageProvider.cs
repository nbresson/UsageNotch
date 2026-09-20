using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>Appelle l'endpoint d'usage OAuth d'Anthropic avec le jeton de Claude Code. Le délai est celui du HttpClient injecté (voir <see cref="Timeout"/>).</summary>
public sealed class ClaudeUsageProvider(HttpClient http, ClaudeCredentialReader credentials, ILogger<ClaudeUsageProvider> logger, TimeProvider time) : IUsageProvider
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    public const string BetaHeader = "oauth-2025-04-20";
    /// <summary>Délai prévu pour l'appel : le Plan 2 doit l'affecter à <see cref="HttpClient.Timeout"/> lors de l'enregistrement du HttpClient.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public string Id => "claude";
    public string DisplayName => "Claude";
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.Claude;

    public async Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var cred = credentials.Read();
        if (cred is null)
        {
            return new FetchResult.NeedsAuth("Aucun identifiant Claude Code trouvé — connectez-vous une fois avec la CLI claude.");
        }

        var result = await FetchOnceAsync(cred.AccessToken, ct);

        if (result is FetchResult.NeedsAuth)
        {
            // Claude Code a peut-être renouvelé le jeton entre-temps : une relecture, un seul nouvel essai.
            var again = credentials.Read();
            if (again is not null && again.AccessToken != cred.AccessToken)
            {
                logger.LogInformation("Jeton Claude changé après un refus, nouvel essai");
                cred = again;
                result = await FetchOnceAsync(again.AccessToken, ct);
            }
        }

        if (result is FetchResult.NeedsAuth)
        {
            return new FetchResult.NeedsAuth(cred.IsExpired
                ? "Identifiant expiré — lancez une commande claude pour le renouveler."
                : "Identifiant refusé (changement de compte ?).");
        }

        return result;
    }

    private async Task<FetchResult> FetchOnceAsync(string token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("anthropic-beta", BetaHeader);

            using var response = await http.SendAsync(request, ct);
            var code = (int)response.StatusCode;
            switch (code)
            {
                case 200:
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var windows = ClaudeUsageParser.Parse(json);
                    if (windows.Count == 0)
                    {
                        logger.LogWarning("Usage Claude : réponse 200 sans fenêtre de limite reconnue");
                        return new FetchResult.Failed("Réponse sans fenêtre de limite");
                    }
                    return new FetchResult.Success(windows);
                case 401:
                case 403:
                    return new FetchResult.NeedsAuth("");
                case 429:
                    var retryAfter = RetryDelay(response.Headers.RetryAfter);
                    logger.LogWarning("Usage Claude : 429, Retry-After {Seconds}s", retryAfter.TotalSeconds);
                    return new FetchResult.RateLimited(retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
                default:
                    logger.LogWarning("Usage Claude : HTTP {Code}", code);
                    return new FetchResult.Failed($"HTTP {code}");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FetchResult.Failed($"Délai dépassé ({(int)http.Timeout.TotalSeconds} s)");
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "Usage Claude : erreur réseau");
            return new FetchResult.Failed(NetworkNote(e));
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Usage Claude : réponse illisible");
            return new FetchResult.Failed("Réponse illisible");
        }
    }

    private static string NetworkNote(HttpRequestException e) =>
        e.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError => "Erreur réseau : nom d'hôte introuvable",
            HttpRequestError.ConnectionError => "Erreur réseau : connexion impossible",
            HttpRequestError.SecureConnectionError => "Erreur réseau : connexion sécurisée impossible",
            _ => "Erreur réseau",
        };

    /// <summary>Retry-After en secondes ou en date HTTP ; une date passée ou absente donne zéro.</summary>
    private TimeSpan RetryDelay(RetryConditionHeaderValue? header)
    {
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is { } date)
        {
            var wait = date - time.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
        return TimeSpan.Zero;
    }
}
