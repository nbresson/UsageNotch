using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class UsageStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitWindow Session = new("session", "Session en cours", 0.73, Now.AddHours(2));

    private static (UsageStore Store, FakeTimeProvider Time) Build(TempDir dir)
    {
        var time = new FakeTimeProvider(Now);
        return (new UsageStore(dir.File("usage.json"), time, NullLogger<UsageStore>.Instance), time);
    }

    [Fact]
    public void Starts_empty()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Current.Should().Be(UsageSnapshot.Empty);
        store.IsInBackoff.Should().BeFalse();
    }

    [Fact]
    public void Success_becomes_Ok_and_is_persisted()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        UsageSnapshot? published = null;
        store.Changed += s => published = s;

        store.Apply(new FetchResult.Success([Session]));

        store.Current.Status.Should().Be(SnapshotStatus.Ok);
        store.Current.Windows.Should().ContainSingle();
        store.Current.FetchedAt.Should().Be(Now);
        store.Current.Note.Should().BeEmpty();
        published.Should().Be(store.Current);
        File.Exists(dir.File("usage.json")).Should().BeTrue();
    }

    [Fact]
    public void Failure_after_a_reading_keeps_the_windows_and_marks_Stale()
    {
        using var dir = new TempDir();
        var (store, time) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));
        time.Advance(TimeSpan.FromMinutes(3));

        store.Apply(new FetchResult.Failed("HTTP 500"));

        store.Current.Status.Should().Be(SnapshotStatus.Stale);
        store.Current.Windows.Should().ContainSingle();
        store.Current.FetchedAt.Should().Be(Now);
        store.Current.Note.Should().Be("HTTP 500");
    }

    [Fact]
    public void Failure_without_any_reading_is_Error()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.Failed("panne"));
        store.Current.Status.Should().Be(SnapshotStatus.Error);
    }

    [Fact]
    public void RateLimited_sets_backoff_deadline_and_Stale_when_a_reading_exists()
    {
        using var dir = new TempDir();
        var (store, time) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));

        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));

        store.Current.Status.Should().Be(SnapshotStatus.Stale);
        store.Current.BackoffUntil.Should().Be(Now.AddSeconds(60));
        store.Current.Note.Should().Contain("60 s");
        store.IsInBackoff.Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(61));
        store.IsInBackoff.Should().BeFalse();
    }

    [Fact]
    public void RateLimited_without_reading_is_Backoff()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        store.Current.Status.Should().Be(SnapshotStatus.Backoff);
    }

    [Fact]
    public void NeedsAuth_keeps_windows_and_clears_backoff()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));

        store.Apply(new FetchResult.NeedsAuth("Identifiant refusé"));

        store.Current.Status.Should().Be(SnapshotStatus.NeedsAuth);
        store.Current.Windows.Should().ContainSingle();
        store.Current.BackoffUntil.Should().BeNull();
    }

    [Fact]
    public void Success_clears_a_previous_backoff()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        store.Apply(new FetchResult.Success([Session]));
        store.Current.BackoffUntil.Should().BeNull();
        store.IsInBackoff.Should().BeFalse();
    }

    [Fact]
    public void ClearBackoff_removes_the_deadline_and_publishes()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        var published = 0;
        store.Changed += _ => published++;

        store.ClearBackoff();

        store.Current.BackoffUntil.Should().BeNull();
        store.Current.Status.Should().Be(SnapshotStatus.Error);
        published.Should().Be(1);
        store.ClearBackoff();
        published.Should().Be(1, "rien à effacer, rien à publier");
    }

    [Fact]
    public void Load_restores_a_persisted_reading_as_Stale_with_its_backoff()
    {
        using var dir = new TempDir();
        var (first, _) = Build(dir);
        first.Apply(new FetchResult.Success([Session]));
        first.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromMinutes(5));

        var (second, _) = Build(dir);
        UsageSnapshot? published = null;
        second.Changed += s => published = s;
        second.Load();

        second.Current.Status.Should().Be(SnapshotStatus.Stale);
        second.Current.Windows.Should().ContainSingle(w => w.Id == "session");
        second.Current.FetchedAt.Should().Be(Now);
        second.Current.BackoffUntil.Should().Be(Now.AddMinutes(5));
        published.Should().NotBeNull();
    }

    [Fact]
    public void Load_ignores_a_missing_or_corrupt_file()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Load();
        store.Current.Should().Be(UsageSnapshot.Empty);

        File.WriteAllText(dir.File("usage.json"), "{ corrupt");
        store.Load();
        store.Current.Should().Be(UsageSnapshot.Empty);
    }

    [Fact]
    public void Persisted_file_does_not_leave_a_temp_file_behind()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));
        Directory.GetFiles(dir.Path).Should().ContainSingle().Which.Should().EndWith("usage.json");
    }

    [Fact]
    public void Multiple_providers_are_stored_independently_and_persisted()
    {
        using var dir = new TempDir();
        var (store, time) = Build(dir);
        var geminiWindow = new LimitWindow("gemini-5h", "Modèles Gemini (5 h)", 0.65, Now.AddHours(3));

        store.Apply("claude", new FetchResult.Success([Session]));
        store.Apply("antigravity", new FetchResult.Success([geminiWindow]));

        store.SnapshotFor("claude").Windows.Should().ContainSingle(w => w.Id == "session");
        store.SnapshotFor("antigravity").Windows.Should().ContainSingle(w => w.Id == "gemini-5h");
        store.Snapshots.Should().HaveCount(2);

        // Reload from disk
        var (second, _) = Build(dir);
        second.Load();

        second.SnapshotFor("claude").Windows.Should().ContainSingle(w => w.Id == "session");
        second.SnapshotFor("claude").Status.Should().Be(SnapshotStatus.Stale);
        second.SnapshotFor("antigravity").Windows.Should().ContainSingle(w => w.Id == "gemini-5h");
        second.SnapshotFor("antigravity").Status.Should().Be(SnapshotStatus.Stale);
    }

    [Fact]
    public void Legacy_single_object_usage_file_is_loaded_as_claude_snapshot()
    {
        using var dir = new TempDir();
        var legacyJson = """
        {
          "status": "Ok",
          "windows": [
            { "id": "session", "label": "Session en cours", "usedFraction": 0.5, "resetsAt": "2026-09-14T14:00:00+00:00" }
          ],
          "fetchedAt": "2026-09-14T12:00:00+00:00",
          "note": ""
        }
        """;
        File.WriteAllText(dir.File("usage.json"), legacyJson);

        var (store, _) = Build(dir);
        store.Load();

        store.SnapshotFor("claude").Windows.Should().ContainSingle(w => w.Id == "session");
        store.SnapshotFor("claude").Status.Should().Be(SnapshotStatus.Stale);
        store.Current.Windows.Should().ContainSingle(w => w.Id == "session");
    }

    [Fact]
    public void IsInBackoff_is_per_provider_and_global()
    {
        using var dir = new TempDir();
        var (store, time) = Build(dir);

        store.Apply("claude", new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        store.Apply("antigravity", new FetchResult.Success([Session]));

        store.IsInBackoffFor("claude").Should().BeTrue();
        store.IsInBackoffFor("antigravity").Should().BeFalse();
        store.IsInBackoff.Should().BeTrue("au moins un fournisseur est en backoff");

        time.Advance(TimeSpan.FromSeconds(61));
        store.IsInBackoffFor("claude").Should().BeFalse();
        store.IsInBackoff.Should().BeFalse();
    }
}
