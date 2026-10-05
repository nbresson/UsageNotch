using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>Interroge l'API de coûts et d'usage de la plateforme OpenAI.</summary>
public sealed class OpenAiUsageProvider(
    HttpClient http,
    OpenAiCredentialReader credentials,
    SettingsStore settings,
    TimeProvider time,
    ILogger<OpenAiUsageProvider> logger) : IUsageProvider
{
    public const string EndpointBase = "https://api.openai.com/v1/organization/costs";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public string Id => "openai";
    public string DisplayName => "OpenAI";
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.OpenAi;

    public async Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var apiKey = credentials.Read();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new FetchResult.NeedsAuth("Aucune clé d'API OpenAI configurée — renseignez OPENAI_API_KEY ou configurez-la dans les réglages.");
        }

        var now = time.GetUtcNow();
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var startTime = startOfMonth.ToUnixTimeSeconds();
        var endTime = now.ToUnixTimeSeconds();

        var url = $"{EndpointBase}?start_time={startTime}&end_time={endTime}&bucket_width=1d";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await http.SendAsync(request, ct);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var budget = settings.Current.OpenAiMonthlyBudget;
                var windows = OpenAiCostParser.Parse(content, budget, now);
                return new FetchResult.Success(windows);
            }

            if (code == 401)
            {
                return new FetchResult.NeedsAuth("Clé d'API OpenAI invalide ou révoquée.");
            }

            if (code == 403)
            {
                return new FetchResult.NeedsAuth("L'API de coûts OpenAI nécessite une clé avec droits d'administration d'organisation (Admin key).");
            }

            if (code == 429)
            {
                TimeSpan? retryAfter = null;
                if (response.Headers.RetryAfter?.Delta is { } delta) retryAfter = delta;
                return new FetchResult.RateLimited(retryAfter ?? TimeSpan.FromSeconds(60));
            }

            logger.LogWarning("Réponse d'erreur inattendue d'OpenAI : {StatusCode}", code);
            return new FetchResult.Failed($"Erreur API OpenAI ({code} {response.ReasonPhrase})");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de l'appel à l'API OpenAI");
            return new FetchResult.Failed($"Échec de connexion à OpenAI : {ex.Message}");
        }
    }
}
