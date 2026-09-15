using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ClaudeUsageProviderTests
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

    private const string Body = """{ "limits": [ { "kind": "session", "percent": 73, "resets_at": "2026-09-14T20:00:00Z" } ] }""";

    private static void WriteToken(TempDir dir, string token) =>
        File.WriteAllText(dir.File(".credentials.json"), $$"""{ "claudeAiOauth": { "accessToken": "{{token}}", "expiresAt": 9999999999999 } }""");

    private static (ClaudeUsageProvider Provider, StubHandler Handler) Build(TempDir dir, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        var reader = new ClaudeCredentialReader(dir.Path, time);
        var provider = new ClaudeUsageProvider(new HttpClient(handler), reader, NullLogger<ClaudeUsageProvider>.Instance, time);
        return (provider, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Returns_NeedsAuth_without_calling_the_network_when_no_credential()
    {
        using var dir = new TempDir();
        var (provider, handler) = Build(dir, _ => Json(HttpStatusCode.OK, Body));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("Aucun identifiant");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Sends_bearer_and_beta_header_and_parses_success()
    {
        using var dir = new TempDir();
        WriteToken(dir, "tok-1");
        var (provider, handler) = Build(dir, _ => Json(HttpStatusCode.OK, Body));

        var result = await provider.FetchAsync(CancellationToken.None);

        var success = result.Should().BeOfType<FetchResult.Success>().Subject;
        success.Windows.Should().ContainSingle(w => w.Id == "session");
        var req = handler.Requests.Single();
        req.RequestUri!.ToString().Should().Be(ClaudeUsageProvider.Endpoint);
        req.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "tok-1"));
        req.Headers.GetValues("anthropic-beta").Should().Equal(ClaudeUsageProvider.BetaHeader);
    }

    [Fact]
    public async Task Rereads_the_credential_once_after_401_and_retries_if_it_changed()
    {
        using var dir = new TempDir();
        WriteToken(dir, "old");
        var (provider, handler) = Build(dir, req =>
        {
            if (req.Headers.Authorization!.Parameter == "old")
            {
                WriteToken(dir, "new"); // Claude Code vient de renouveler le jeton
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            return Json(HttpStatusCode.OK, Body);
        });

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Success>();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Returns_NeedsAuth_when_401_and_the_credential_did_not_change()
    {
        using var dir = new TempDir();
        WriteToken(dir, "same");
        var (provider, handler) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("refusé");
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Mentions_expiry_in_the_NeedsAuth_note_when_the_token_is_expired()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), """{ "claudeAiOauth": { "accessToken": "exp", "expiresAt": 1 } }""");
        var (provider, _) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("expiré");
    }

    [Fact]
    public async Task Returns_RateLimited_with_retry_after_on_429()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return r;
        });

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>().Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Theory]
    [InlineData(90, 90)]
    [InlineData(-30, 0)]
    public async Task Returns_RateLimited_with_the_delay_until_an_http_date_retry_after(int offsetSeconds, int expectedSeconds)
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var (provider, _) = Build(dir, _ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(offsetSeconds));
            return r;
        });

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>().Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public async Task Returns_RateLimited_zero_when_429_has_no_retry_after()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>().Which.RetryAfter.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Returns_Failed_with_status_code_on_other_http_errors()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("HTTP 500");
    }

    [Fact]
    public async Task Returns_Failed_on_unreadable_body()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => Json(HttpStatusCode.OK, "{ nope"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("Réponse illisible");
    }

    [Fact]
    public async Task Returns_Failed_on_network_exception()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => throw new HttpRequestException("connexion refusée"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("Erreur réseau");
    }

    [Fact]
    public async Task Returns_a_specific_French_note_for_a_connection_error()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("Erreur réseau : connexion impossible");
    }

    [Fact]
    public async Task Returns_Failed_when_the_response_has_no_limit_window()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => Json(HttpStatusCode.OK, "{}"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("Réponse sans fenêtre de limite");
    }

    [Fact]
    public async Task The_timeout_note_reports_the_client_timeout()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var reader = new ClaudeCredentialReader(dir.Path, new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero)));
        var http = new HttpClient(new HangingHandler()) { Timeout = TimeSpan.FromSeconds(1) };
        var provider = new ClaudeUsageProvider(http, reader, NullLogger<ClaudeUsageProvider>.Instance, TimeProvider.System);

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("Délai dépassé (1 s)");
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
