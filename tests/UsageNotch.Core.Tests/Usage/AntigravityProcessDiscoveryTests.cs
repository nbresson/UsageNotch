using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class AntigravityProcessDiscoveryTests
{
    private class FakeInspector(params string[] lines) : IAntigravityProcessInspector
    {
        public IEnumerable<string> Lines { get; set; } = lines;
        public int CallCount { get; private set; }

        public IEnumerable<string> GetCandidateCommandLines()
        {
            CallCount++;
            return Lines;
        }
    }

    [Fact]
    public void Extracts_port_and_csrf_token_from_standard_command_line()
    {
        var line = "\"C:\\Users\\user\\.gemini\\bin\\agy.exe\" --app_data_dir=antigravity --hub --hub-port=37588 --csrf_token=c47a09fb50f044359d9b8c7ba430adcd --log-file=\"C:\\logs\\vs_ls.log\"";
        var inspector = new FakeInspector(line);
        var discovery = new AntigravityProcessDiscovery(inspector);

        var info = discovery.Discover();

        info.Should().NotBeNull();
        info!.Port.Should().Be(37588);
        info.CsrfToken.Should().Be("c47a09fb50f044359d9b8c7ba430adcd");
    }

    [Fact]
    public void Returns_null_when_no_candidate_processes_exist()
    {
        var inspector = new FakeInspector();
        var discovery = new AntigravityProcessDiscovery(inspector);

        discovery.Discover().Should().BeNull();
    }

    [Fact]
    public void Returns_null_when_port_is_missing()
    {
        var line = "agy.exe --hub --csrf_token=c47a09fb50f044359d9b8c7ba430adcd";
        var inspector = new FakeInspector(line);
        var discovery = new AntigravityProcessDiscovery(inspector);

        discovery.Discover().Should().BeNull();
    }

    [Fact]
    public void Returns_null_when_csrf_token_is_missing()
    {
        var line = "agy.exe --hub --hub-port=37588";
        var inspector = new FakeInspector(line);
        var discovery = new AntigravityProcessDiscovery(inspector);

        discovery.Discover().Should().BeNull();
    }

    [Fact]
    public void Reuses_cached_result_on_subsequent_calls_without_calling_inspector()
    {
        var line = "agy.exe --hub-port=12345 --csrf_token=abc123";
        var inspector = new FakeInspector(line);
        var discovery = new AntigravityProcessDiscovery(inspector);

        var first = discovery.Discover();
        var second = discovery.Discover();

        first.Should().Be(second);
        inspector.CallCount.Should().Be(1);
    }

    [Fact]
    public void InvalidateCache_causes_next_call_to_reinspect()
    {
        var line1 = "agy.exe --hub-port=12345 --csrf_token=token1";
        var inspector = new FakeInspector(line1);
        var discovery = new AntigravityProcessDiscovery(inspector);

        discovery.Discover()!.Port.Should().Be(12345);

        discovery.InvalidateCache();
        inspector.Lines = ["agy.exe --hub-port=54321 --csrf_token=token2"];

        var updated = discovery.Discover();
        updated!.Port.Should().Be(54321);
        updated.CsrfToken.Should().Be("token2");
        inspector.CallCount.Should().Be(2);
    }

    [Fact]
    public void Real_system_discovery_succeeds_when_agy_is_running()
    {
        if (System.Diagnostics.Process.GetProcessesByName("agy").Length == 0) return;

        var discovery = new AntigravityProcessDiscovery();
        var hub = discovery.Discover();
        hub.Should().NotBeNull();
        hub!.Port.Should().BeGreaterThan(0);
        hub.CsrfToken.Should().NotBeNullOrWhiteSpace();
    }
}
