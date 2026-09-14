using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class UsagePollerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitWindow Session = new("session", "Session en cours", 0.5, Now.AddHours(1));

    private sealed class FakeProvider : IUsageProvider
    {
        public Queue<FetchResult> Results { get; } = new();
        public int Calls { get; private set; }
        public string Id => "claude";
        public string DisplayName => "Claude";
        public string HeadlineWindowId => "session";
        public Task<FetchResult> FetchAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new FetchResult.Success([Session]));
        }
    }

    private sealed class FakeActivity : ISessionActivity
    {
        public bool HasActiveSession { get; set; }
    }

    private static (UsagePoller Poller, FakeProvider Provider, UsageStore Store, FakeActivity Activity, FakeTimeProvider Time) Build(TempDir dir)
    {
        var time = new FakeTimeProvider(Now);
        var provider = new FakeProvider();
        var store = new UsageStore(dir.File("usage.json"), time, NullLogger<UsageStore>.Instance);
        var activity = new FakeActivity();
        var poller = new UsagePoller(provider, store, activity, time, NullLogger<UsagePoller>.Instance);
        return (poller, provider, store, activity, time);
    }

    [Fact]
    public async Task A_tick_fetches_and_applies_a_success()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, _) = Build(dir);

        await poller.TickAsync(CancellationToken.None);

        provider.Calls.Should().Be(1);
        store.Current.Status.Should().Be(SnapshotStatus.Ok);
    }

    [Fact]
    public async Task No_call_is_made_during_a_backoff()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, time) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(Now.AddSeconds(60));

        time.Advance(TimeSpan.FromSeconds(30));
        await poller.TickAsync(CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Consecutive_429s_double_the_wait_and_a_success_resets_it()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, time) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        provider.Results.Enqueue(new FetchResult.Success([Session]));
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));

        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(time.GetUtcNow().AddSeconds(60));

        time.Advance(TimeSpan.FromSeconds(61));
        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(time.GetUtcNow().AddSeconds(120));

        time.Advance(TimeSpan.FromSeconds(121));
        await poller.TickAsync(CancellationToken.None);
        store.Current.Status.Should().Be(SnapshotStatus.Ok);

        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(time.GetUtcNow().AddSeconds(60), "le compteur a été remis à zéro par le succès");
    }

    [Fact]
    public async Task RequestRefresh_clears_the_backoff_so_the_next_tick_calls()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, _) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        await poller.TickAsync(CancellationToken.None);

        poller.RequestRefresh();
        store.IsInBackoff.Should().BeFalse();
        await poller.TickAsync(CancellationToken.None);

        provider.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Retry_after_raises_the_backoff_floor()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, _) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.FromSeconds(600)));
        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(Now.AddSeconds(600));
    }

    [Fact]
    public void Interval_is_60s_with_an_active_session_and_5min_otherwise()
    {
        using var dir = new TempDir();
        var (poller, _, _, activity, _) = Build(dir);
        activity.HasActiveSession = true;
        poller.NextInterval().Should().Be(TimeSpan.FromSeconds(60));
        activity.HasActiveSession = false;
        poller.NextInterval().Should().Be(TimeSpan.FromMinutes(5));
    }
}
