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

    private const string SuccessBody = """
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

    private static (OpenAiUsageProvider Provider, StubHandler Handler, SettingsStore Store) Build(
        TempDir dir,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string? apiKey = "sk-test-key",
        double budget = 50.0)
    {
        var handler = new StubHandler(respond);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 5, 14, 0, 0, TimeSpan.Zero));
        var store = new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);
        if (apiKey != null || budget != 20.0)
        {
            store.Save(store.Current with { OpenAiApiKey = apiKey ?? "", OpenAiMonthlyBudget = budget });
        }
        var reader = new OpenAiCredentialReader(store);
        var provider = new OpenAiUsageProvider(new HttpClient(handler), reader, store, time, NullLogger<OpenAiUsageProvider>.Instance);
        return (provider, handler, store);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Returns_NeedsAuth_without_network_call_when_no_api_key()
    {
        using var dir = new TempDir();
        var (provider, handler, _) = Build(dir, _ => Json(HttpStatusCode.OK, SuccessBody), apiKey: null);

        // Ensure env var is also not set
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
    public async Task Sends_Bearer_token_and_parses_success_response()
    {
        using var dir = new TempDir();
        var (provider, handler, _) = Build(dir, _ => Json(HttpStatusCode.OK, SuccessBody), apiKey: "sk-my-secret-key");

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
    public async Task Returns_NeedsAuth_on_401_unauthorized()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ => Json(HttpStatusCode.Unauthorized, "{}"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>()
            .Which.Note.Should().Contain("invalide ou révoquée");
    }

    [Fact]
    public async Task Returns_NeedsAuth_on_403_forbidden_requiring_admin()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ => Json(HttpStatusCode.Forbidden, "{}"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>()
            .Which.Note.Should().Contain("droits d'administration");
    }

    [Fact]
    public async Task Returns_RateLimited_on_429_too_many_requests()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ =>
        {
            var res = Json(HttpStatusCode.TooManyRequests, "{}");
            res.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
            return res;
        });

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>()
            .Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task Returns_Failed_on_500_server_error()
    {
        using var dir = new TempDir();
        var (provider, _, _) = Build(dir, _ => Json(HttpStatusCode.InternalServerError, "Error"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>()
            .Which.Note.Should().Contain("500");
    }
}
