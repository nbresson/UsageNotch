using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class AntigravityUsageProviderTests
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

    private class FakeInspector(params string[] lines) : IAntigravityProcessInspector
    {
        public IEnumerable<string> Lines { get; set; } = lines;

        public IEnumerable<string> GetCandidateCommandLines() => Lines;
    }

    private const string ValidBody = """
    {
      "response": {
        "groups": [
          {
            "displayName": "Gemini Models",
            "buckets": [
              { "bucketId": "gemini-5h", "remainingFraction": 0.5, "resetTime": "2026-10-05T17:50:04Z" }
            ]
          }
        ]
      }
    }
    """;

    private static (AntigravityUsageProvider Provider, StubHandler Handler, FakeInspector Inspector) Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        params string[] commandLines)
    {
        var handler = new StubHandler(respond);
        var inspector = new FakeInspector(commandLines);
        var discovery = new AntigravityProcessDiscovery(inspector);
        var provider = new AntigravityUsageProvider(new HttpClient(handler), discovery, NullLogger<AntigravityUsageProvider>.Instance);
        return (provider, handler, inspector);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Returns_NeedsAuth_without_network_call_when_process_not_found()
    {
        var (provider, handler, _) = Build(_ => Json(HttpStatusCode.OK, ValidBody));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("Antigravity");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Sends_post_with_csrf_header_and_parses_success()
    {
        var (provider, handler, _) = Build(
            _ => Json(HttpStatusCode.OK, ValidBody),
            "agy.exe --hub-port=37588 --csrf_token=my-token");

        var result = await provider.FetchAsync(CancellationToken.None);

        var success = result.Should().BeOfType<FetchResult.Success>().Subject;
        success.Windows.Should().ContainSingle(w => w.Id == "gemini-5h");

        var req = handler.Requests.Single();
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.ToString().Should().Be("http://127.0.0.1:37588/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary");
        req.Headers.GetValues("x-codeium-csrf-token").Should().Equal("my-token");
    }

    [Fact]
    public async Task Invalidate_cache_and_retries_once_after_401_if_new_process_info_found()
    {
        var inspector = new FakeInspector("agy.exe --hub-port=37588 --csrf_token=token-1");
        var handler = new StubHandler(req =>
        {
            if (req.Headers.GetValues("x-codeium-csrf-token").First() == "token-1")
            {
                // Antigravity a redémarré avec un nouveau token
                inspector.Lines = ["agy.exe --hub-port=37588 --csrf_token=token-2"];
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            return Json(HttpStatusCode.OK, ValidBody);
        });
        var discovery = new AntigravityProcessDiscovery(inspector);
        var provider = new AntigravityUsageProvider(new HttpClient(handler), discovery, NullLogger<AntigravityUsageProvider>.Instance);

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Success>();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Returns_NeedsAuth_when_401_and_no_new_process_info_found()
    {
        var (provider, handler, _) = Build(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            "agy.exe --hub-port=37588 --csrf_token=same-token");

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>();
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Invalidate_cache_and_returns_Failed_on_network_error()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("Connexion refusée"));
        var inspector = new FakeInspector("agy.exe --hub-port=37588 --csrf_token=tok");
        var discovery = new AntigravityProcessDiscovery(inspector);
        var provider = new AntigravityUsageProvider(new HttpClient(handler), discovery, NullLogger<AntigravityUsageProvider>.Instance);

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Contain("Connexion impossible");
        inspector.Lines = [];
        discovery.Discover().Should().BeNull(); // Le cache a été invalidé et l'inspecteur n'a plus de processus
    }

    [Fact]
    public async Task Real_antigravity_fetch_succeeds_when_agy_is_running()
    {
        if (System.Diagnostics.Process.GetProcessesByName("agy").Length == 0) return;

        var discovery = new AntigravityProcessDiscovery();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var provider = new AntigravityUsageProvider(client, discovery, NullLogger<AntigravityUsageProvider>.Instance);

        var result = await provider.FetchAsync(CancellationToken.None);
        result.Should().BeOfType<FetchResult.Success>();
        var success = (FetchResult.Success)result;
        success.Windows.Should().NotBeEmpty();
    }
}
