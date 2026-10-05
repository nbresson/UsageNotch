using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class OpenAiUsageProviderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private const string ApiSuccessBody = """
    {
      "data": [
        {
          "amount": 10.0,
          "start_time": 1791208200,
          "line_item": "gpt-4o"
        }
      ]
    }
    """;

    private const string SubscriptionSuccessBody = """
    {
      "plan_type": "plus",
      "rate_limit": {
        "primary_window": {
          "used_percent": 30.0,
          "limit_window_seconds": 18000,
          "reset_after_seconds": 3600
        },
        "secondary_window": {
          "used_percent": 50.0,
          "limit_window_seconds": 604800,
          "reset_after_seconds": 72000
        }
      }
    }
    """;

    private static (OpenAiUsageProvider Provider, StubHandler Handler, SettingsStore Store) Build(
        TempDir dir,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string mode = "api",
        string? apiKey = "sk-test-key",
        double budget = 50.0,
        string? sessionToken = null,
        string? accountId = null)
    {
        var handler = new StubHandler(respond);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 5, 14, 0, 0, TimeSpan.Zero));
        var store = new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);

        store.Save(store.Current with
        {
            OpenAiMode = mode,
            OpenAiApiKey = apiKey ?? "",
            OpenAiMonthlyBudget = budget,
            OpenAiSessionToken = sessionToken ?? "",
            OpenAiAccountId = accountId ?? ""
        });

        var credReader = new OpenAiCredentialReader(store);
        var subReader = new OpenAiSubscriptionCredentialReader(store, codexHomeOverride: dir.Path);
        var provider = new OpenAiUsageProvider(new HttpClient(handler), credReader, subReader, store, time, NullLogger<OpenAiUsageProvider>.Instance);
        return (provider, handler, store);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    #region API Mode Tests

    [Fact]
    public async Task ApiMode_Returns_NeedsAuth_without_network_call_when_no_api_key()
    {
        using var dir = new TempDir();
        var (provider, handler, _) = Build(dir, _ => Json(HttpStatusCode.OK, ApiSuccessBody), mode: "api", apiKey: null);

        var prevEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            var result = await provider.FetchAsync(CancellationToken.None);

            result.Should().BeOfType<FetchResult.NeedsAuth>()
                .Which.Note.Should().Contain("Aucune clé d'API OpenAI");
            handler.Requests.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevEnv);
        }
    }

    [Fact]
    public async Task ApiMode_Sends_Bearer_token_and_parses_success_response()
    {
        using var dir = new TempDir();
        var (provider, handler, _) = Build(dir, _ => Json(HttpStatusCode.OK, ApiSuccessBody), mode: "api", apiKey: "sk-my-secret-key");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Success>();
        var success = (FetchResult.Success)result;
        success.Windows.Should().HaveCount(3);

        handler.Requests.Should().HaveCount(1);
        var req = handler.Requests[0];
        req.Headers.Authorization.Should().NotBeNull();
        req.Headers.Authorization!.Scheme.Should().Be("Bearer");
        req.Headers.Authorization!.Parameter.Should().Be("sk-my-secret-key");
        req.RequestUri!.ToString().Should().Contain("start_time=").And.Contain("bucket_width=1d");
    }

    [Fact]
    public async Task ApiMode_Returns_NeedsAuth_on_401_unauthorized()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ => Json(HttpStatusCode.Unauthorized, "{}"), mode: "api");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>()
            .Which.Note.Should().Contain("invalide ou révoquée");
    }

    [Fact]
    public async Task ApiMode_Returns_NeedsAuth_on_403_forbidden_requiring_admin()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ => Json(HttpStatusCode.Forbidden, "{}"), mode: "api");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>()
            .Which.Note.Should().Contain("droits d'administration");
    }

    [Fact]
    public async Task ApiMode_Returns_RateLimited_on_429_too_many_requests()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ =>
        {
            var res = Json(HttpStatusCode.TooManyRequests, "{}");
            res.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
            return res;
        }, mode: "api");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>()
            .Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task ApiMode_Returns_Failed_on_500_server_error()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ => Json(HttpStatusCode.InternalServerError, "Error"), mode: "api");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>()
            .Which.Note.Should().Contain("500");
    }

    #endregion

    #region Subscription Mode Tests

    [Fact]
    public async Task SubscriptionMode_Returns_NeedsAuth_without_network_call_when_no_token()
    {
        using var dir = new TempDir();
        var (provider, handler, _) = Build(dir, _ => Json(HttpStatusCode.OK, SubscriptionSuccessBody), mode: "subscription", sessionToken: null);

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>()
            .Which.Note.Should().Contain("Aucun token de session OpenAI/ChatGPT trouvé");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionMode_Sends_token_and_account_id_and_parses_success()
    {
        using var dir = new TempDir();
        var (provider, handler, _) = Build(
            dir,
            _ => Json(HttpStatusCode.OK, SubscriptionSuccessBody),
            mode: "subscription",
            sessionToken: "sess-token-abc",
            accountId: "org-account-123");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Success>();
        var success = (FetchResult.Success)result;
        success.Windows.Should().HaveCount(3);
        success.Windows.First(w => w.Id == "session").UsedFraction.Should().BeApproximately(0.30, 0.001);
        success.Windows.First(w => w.Id == "weekly").UsedFraction.Should().BeApproximately(0.50, 0.001);

        handler.Requests.Should().HaveCount(1);
        var req = handler.Requests[0];
        req.RequestUri!.ToString().Should().Be(OpenAiUsageProvider.EndpointSubscription);
        req.Headers.Authorization!.Scheme.Should().Be("Bearer");
        req.Headers.Authorization!.Parameter.Should().Be("sess-token-abc");
        req.Headers.GetValues("ChatGPT-Account-Id").Should().Contain("org-account-123");
    }

    [Fact]
    public async Task SubscriptionMode_Returns_NeedsAuth_on_401()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(
            dir,
            _ => Json(HttpStatusCode.Unauthorized, "{}"),
            mode: "subscription",
            sessionToken: "expired-token");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>()
            .Which.Note.Should().Contain("expirée ou invalide");
    }

    [Fact]
    public async Task SubscriptionMode_Returns_RateLimited_on_429()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(
            dir,
            _ =>
            {
                var res = Json(HttpStatusCode.TooManyRequests, "{}");
                res.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
                return res;
            },
            mode: "subscription",
            sessionToken: "valid-token");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>()
            .Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task SubscriptionMode_Returns_Failed_on_500()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(
            dir,
            _ => Json(HttpStatusCode.InternalServerError, "Error"),
            mode: "subscription",
            sessionToken: "valid-token");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>()
            .Which.Note.Should().Contain("500");
    }

    #endregion
}
