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

    [Fact]
    public void Registry_resolves_and_lists_providers()
    {
        var claude = new FakeProvider("claude", "Claude", RingWindows.Claude);
        var openAi = new FakeProvider("openai", "OpenAI", RingWindows.OpenAi);
        var registry = new UsageProviderRegistry([claude, openAi]);

        registry.All.Should().HaveCount(2);
        registry.GetProvider("claude").Should().BeSameAs(claude);
        registry.GetProvider("CLAUDE").Should().BeSameAs(claude);
        registry.GetProvider("openai").Should().BeSameAs(openAi);
        registry.GetProvider("unknown").Should().BeNull();
    }

    [Fact]
    public void Router_works_with_registry()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);
        store.Load();
        store.Save(store.Current with { Provider = "openai" });

        var claude = new FakeProvider("claude", "Claude", RingWindows.Claude);
        var openAi = new FakeProvider("openai", "OpenAI", RingWindows.OpenAi);
        var registry = new UsageProviderRegistry([claude, openAi]);

        var router = new RoutingUsageProvider(store, registry);
        router.Id.Should().Be("openai");
        router.DisplayName.Should().Be("OpenAI");
    }
}
