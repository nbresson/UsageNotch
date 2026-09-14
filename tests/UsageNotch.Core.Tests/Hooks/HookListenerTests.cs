using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Hooks;

public class HookListenerTests : IAsyncLifetime
{
    private readonly SessionStore _sessions = new(new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero)));
    private HookListener _listener = null!;
    private HttpClient _client = null!;
    private int _port;

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async Task InitializeAsync()
    {
        _port = FreePort();
        _listener = new HookListener(_port, _sessions, NullLogger<HookListener>.Instance);
        await _listener.StartAsync(CancellationToken.None);
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}/") };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _listener.StopAsync(CancellationToken.None);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task A_hook_post_creates_a_session()
    {
        var response = await _client.PostAsync("event?e=running&ppid=4242",
            Json("""{ "session_id": "s-1", "cwd": "C:\\src\\app", "prompt": "hello" }"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("ok");
        var s = _sessions.Snapshot().Single();
        s.Id.Should().Be("s-1");
        s.State.Should().Be(SessionState.Running);
        s.ParentPid.Should().Be(4242);
        s.Prompt.Should().Be("hello");
    }

    [Fact]
    public async Task A_cross_site_browser_request_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "event?e=running") { Content = Json("{}") };
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.com");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _sessions.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task Sec_fetch_site_cross_site_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "event?e=running") { Content = Json("{}") };
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Open_settings_raises_the_event()
    {
        var raised = 0;
        _listener.OpenSettingsRequested += () => raised++;

        var response = await _client.PostAsync("open-settings", Json(""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raised.Should().Be(1);
    }

    [Fact]
    public async Task Unknown_routes_are_404()
    {
        var response = await _client.GetAsync("nothing");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unreadable_body_is_still_accepted()
    {
        var response = await _client.PostAsync("event?e=done", new StringContent("not json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _sessions.Snapshot().Single().Id.Should().Be("unknown");
    }

    [Fact]
    public async Task An_oversized_body_keeps_its_session_and_gets_a_response()
    {
        var body = "{\"session_id\":\"s-big\",\"cwd\":\"C:\\\\big\",\"tool_name\":\"Write\",\"tool_input\":{\"content\":\""
            + new string('a', 300_000) + "\"}}";

        var response = await _client.PostAsync("event?e=running&ppid=1", Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("ok");
        var s = _sessions.Snapshot().Single();
        s.Id.Should().Be("s-big");
        s.State.Should().Be(SessionState.Running);
    }

    [Fact]
    public async Task A_get_request_is_405_and_changes_nothing()
    {
        var raised = 0;
        _listener.OpenSettingsRequested += () => raised++;

        var ev = await _client.GetAsync("event?e=running");
        var open = await _client.GetAsync("open-settings");

        ev.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        open.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        _sessions.Snapshot().Should().BeEmpty();
        raised.Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_event_kind_is_400_and_changes_nothing()
    {
        var response = await _client.PostAsync("event?e=ping&ppid=1", Json("""{ "session_id": "s-1" }"""));
        var missing = await _client.PostAsync("event", Json("""{ "session_id": "s-2" }"""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Be("unknown event");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _sessions.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task Is_listening_after_start_and_a_second_listener_on_the_same_port_is_not()
    {
        _listener.IsListening.Should().BeTrue();

        using var second = new HookListener(_port, new SessionStore(TimeProvider.System), NullLogger<HookListener>.Instance);
        await second.StartAsync(CancellationToken.None);
        try
        {
            second.IsListening.Should().BeFalse();
        }
        finally
        {
            await second.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Is_not_listening_after_stop()
    {
        var port = FreePort();
        using var listener = new HookListener(port, new SessionStore(TimeProvider.System), NullLogger<HookListener>.Instance);
        await listener.StartAsync(CancellationToken.None);
        listener.IsListening.Should().BeTrue();

        await listener.StopAsync(CancellationToken.None);

        listener.IsListening.Should().BeFalse();
    }
}
