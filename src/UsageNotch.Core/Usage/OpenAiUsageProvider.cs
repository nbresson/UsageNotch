using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Security;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>Interroge l'API de coûts ou les quotas d'abonnement de la plateforme OpenAI.</summary>
public sealed class OpenAiUsageProvider : IUsageProvider
{
    public const string EndpointApiCosts = "https://api.openai.com/v1/organization/costs";
    public const string EndpointSubscription = "https://chatgpt.com/backend-api/wham/usage";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly OpenAiCredentialReader _credentials;
    private readonly OpenAiSubscriptionCredentialReader _subscriptionCredentials;
    private readonly SettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<OpenAiUsageProvider> _logger;

    public OpenAiUsageProvider(
        HttpClient http,
        OpenAiCredentialReader credentials,
        OpenAiSubscriptionCredentialReader subscriptionCredentials,
        SettingsStore settings,
        TimeProvider time,
        ILogger<OpenAiUsageProvider> logger)
    {
        _http = http;
        _credentials = credentials;
        _subscriptionCredentials = subscriptionCredentials;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public OpenAiUsageProvider(
        HttpClient http,
        OpenAiCredentialReader credentials,
        SettingsStore settings,
        TimeProvider time,
        ILogger<OpenAiUsageProvider> logger)
        : this(http, credentials, new OpenAiSubscriptionCredentialReader(settings), settings, time, logger)
    {
    }

    public string Id => "openai";
    public string DisplayName => "OpenAI";
    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => RingWindows.OpenAi;

    public async Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var mode = _settings.Current.OpenAiMode;
        return mode == "api"
            ? await FetchApiCostsAsync(ct)
            : await FetchSubscriptionAsync(ct);
    }

    private async Task<FetchResult> FetchSubscriptionAsync(CancellationToken ct)
    {
        var creds = _subscriptionCredentials.Read();
        if (creds == null || string.IsNullOrWhiteSpace(creds.AccessToken))
        {
            return new FetchResult.NeedsAuth("Aucun token de session OpenAI/ChatGPT trouvé — connectez-vous avec Codex CLI ou renseignez votre token dans les réglages.");
        }

        var now = _time.GetUtcNow();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, EndpointSubscription);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
            if (!string.IsNullOrWhiteSpace(creds.AccountId))
            {
                request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", creds.AccountId);
            }
            request.Headers.UserAgent.ParseAdd("UsageNotch/1.0");

            using var response = await _http.SendAsync(request, ct);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var windows = OpenAiSubscriptionParser.Parse(content, now);
                return new FetchResult.Success(windows);
            }

            if (code == 401)
            {
                return new FetchResult.NeedsAuth("Session OpenAI/ChatGPT expirée ou invalide — veuillez vous reconnecter.");
            }

            if (code == 429)
            {
                TimeSpan? retryAfter = null;
                if (response.Headers.RetryAfter?.Delta is { } delta) retryAfter = delta;
                return new FetchResult.RateLimited(retryAfter ?? TimeSpan.FromSeconds(60));
            }

            if (code >= 500 && response.Headers.RetryAfter?.Delta is { } serverDelta)
            {
                _logger.LogWarning("Serveur OpenAI temporairement indisponible (Retry-After) : {StatusCode}", code);
                return new FetchResult.RateLimited(serverDelta);
            }

            _logger.LogWarning("Réponse d'erreur inattendue de l'abonnement OpenAI : {StatusCode}", code);
            return new FetchResult.Failed($"Erreur API OpenAI ({code} {response.ReasonPhrase})");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'appel à l'API d'abonnement OpenAI");
            return new FetchResult.Failed($"Échec de connexion à OpenAI : {ExceptionSanitizer.Sanitize(ex)}");
        }
    }

    private async Task<FetchResult> FetchApiCostsAsync(CancellationToken ct)
    {
        var apiKey = _credentials.Read();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new FetchResult.NeedsAuth("Aucune clé d'API OpenAI configurée — renseignez OPENAI_API_KEY ou configurez-la dans les réglages.");
        }

        var now = _time.GetUtcNow();
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var startTime = startOfMonth.ToUnixTimeSeconds();
        var endTime = now.ToUnixTimeSeconds();

        var url = $"{EndpointApiCosts}?start_time={startTime}&end_time={endTime}&bucket_width=1d";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _http.SendAsync(request, ct);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var budget = _settings.Current.OpenAiMonthlyBudget;
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

            if (code >= 500 && response.Headers.RetryAfter?.Delta is { } serverDelta)
            {
                _logger.LogWarning("Serveur OpenAI (coûts) temporairement indisponible (Retry-After) : {StatusCode}", code);
                return new FetchResult.RateLimited(serverDelta);
            }

            _logger.LogWarning("Réponse d'erreur inattendue d'OpenAI : {StatusCode}", code);
            return new FetchResult.Failed($"Erreur API OpenAI ({code} {response.ReasonPhrase})");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'appel à l'API OpenAI");
            return new FetchResult.Failed($"Échec de connexion à OpenAI : {ExceptionSanitizer.Sanitize(ex)}");
        }
    }
}
