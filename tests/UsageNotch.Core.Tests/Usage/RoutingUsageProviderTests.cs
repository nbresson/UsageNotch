using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class RoutingUsageProviderTests
{
    private sealed class FakeProvider(string id, string displayName, IReadOnlyList<IReadOnlyList<string>> ringWindowIds) : IUsageProvider
    {
        public int FetchCalls { get; private set; }
        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => ringWindowIds;
        public Task<FetchResult> FetchAsync(CancellationToken ct)
        {
            FetchCalls++;
            return Task.FromResult<FetchResult>(new FetchResult.Success([]));
        }
    }

    [Fact]
    public async Task Delegates_to_active_provider_based_on_settings()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);
        store.Load();

        var claude = new FakeProvider("claude", "Claude", RingWindows.Claude);
        var antigravity = new FakeProvider("antigravity", "Google Antigravity", RingWindows.Antigravity);

        var router = new RoutingUsageProvider(store, id => id switch
        {
            "antigravity" => antigravity,
            _ => claude,
        });

        // Par défaut, claude
        router.Id.Should().Be("claude");
        router.DisplayName.Should().Be("Claude");
        router.RingWindowIds.Should().BeSameAs(RingWindows.Claude);

        await router.FetchAsync(CancellationToken.None);
        claude.FetchCalls.Should().Be(1);
        antigravity.FetchCalls.Should().Be(0);

        // Bascule vers antigravity
        store.Save(store.Current with { Provider = "antigravity" });

        router.Id.Should().Be("antigravity");
        router.DisplayName.Should().Be("Google Antigravity");
        router.RingWindowIds.Should().BeSameAs(RingWindows.Antigravity);

        await router.FetchAsync(CancellationToken.None);
        claude.FetchCalls.Should().Be(1);
        antigravity.FetchCalls.Should().Be(1);
    }
}
