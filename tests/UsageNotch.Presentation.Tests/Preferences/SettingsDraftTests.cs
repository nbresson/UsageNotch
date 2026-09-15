using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public sealed class SettingsDraftTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly SettingsStore _store;
    private int _saves;

    public SettingsDraftTests()
    {
        _store = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _store.Load();
        _store.Changed += _ => _saves++;
    }

    public void Dispose() => _dir.Dispose();

    private SettingsDraft NewDraft() => new(_store, new ImmediateDispatcher(), _time);

    [Fact]
    public void Starts_from_the_saved_settings()
    {
        using var draft = NewDraft();

        draft.Value.Should().Be(_store.Current);
        draft.HasPendingEdits.Should().BeFalse();
    }

    [Fact]
    public void An_edit_is_visible_at_once_and_saved_after_the_commit_delay()
    {
        using var draft = NewDraft();
        var changed = 0;
        draft.Changed += () => changed++;

        draft.Edit(s => s with { Scale = 1.2 });

        draft.Value.Scale.Should().Be(1.2);
        draft.HasPendingEdits.Should().BeTrue();
        changed.Should().Be(1);
        _store.Current.Scale.Should().Be(1.0);

        _time.Advance(SettingsDraft.CommitDelay - TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(0);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(1);
        _store.Current.Scale.Should().Be(1.2);
        draft.HasPendingEdits.Should().BeFalse();

        var reread = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        reread.Load().Scale.Should().Be(1.2);
    }

    [Fact]
    public void A_burst_of_edits_is_saved_together_at_most_every_commit_delay()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.1 });
        _time.Advance(TimeSpan.FromMilliseconds(100));
        draft.Edit(s => s with { Scale = 1.2 });
        _time.Advance(TimeSpan.FromMilliseconds(100));
        draft.Edit(s => s with { Scale = 1.3 });
        _time.Advance(TimeSpan.FromMilliseconds(50));

        _saves.Should().Be(1);
        _store.Current.Scale.Should().Be(1.3);

        draft.Edit(s => s with { Scale = 1.4 });
        _time.Advance(SettingsDraft.CommitDelay - TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(1);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _saves.Should().Be(2);
        _store.Current.Scale.Should().Be(1.4);
    }

    [Fact]
    public void A_change_saved_elsewhere_keeps_the_pending_edits()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.2 });
        _store.Save(_store.Current with { Edge = ScreenEdge.Left });

        draft.Value.Edge.Should().Be(ScreenEdge.Left);
        draft.Value.Scale.Should().Be(1.2);

        _time.Advance(SettingsDraft.CommitDelay);
        _store.Current.Edge.Should().Be(ScreenEdge.Left);
        _store.Current.Scale.Should().Be(1.2);
    }

    [Fact]
    public void Without_pending_edits_an_outside_change_replaces_the_value_and_notifies()
    {
        using var draft = NewDraft();
        var changed = 0;
        draft.Changed += () => changed++;

        _store.Save(_store.Current with { AutoOpenCard = false });

        draft.Value.AutoOpenCard.Should().BeFalse();
        changed.Should().Be(1);
    }

    [Fact]
    public void Quit_turning_auto_launch_off_survives_a_later_flush()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.2 });
        _store.Save(_store.Current with { AutoLaunch = false });
        draft.Flush();

        _store.Current.AutoLaunch.Should().BeFalse();
        _store.Current.Scale.Should().Be(1.2);
    }

    [Fact]
    public void Flush_saves_at_once_and_cancels_the_timer()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 1.2 });
        draft.Flush();

        _saves.Should().Be(1);
        _store.Current.Scale.Should().Be(1.2);

        _time.Advance(SettingsDraft.CommitDelay * 4);
        _saves.Should().Be(1);
    }

    [Fact]
    public void Flush_without_pending_edits_saves_nothing()
    {
        using var draft = NewDraft();

        draft.Flush();

        _saves.Should().Be(0);
    }

    [Fact]
    public void Values_are_clamped_as_they_will_be_when_saved()
    {
        using var draft = NewDraft();

        draft.Edit(s => s with { Scale = 9.0, FoldedThicknessPx = 0 });

        draft.Value.Scale.Should().Be(Settings.ScaleMax);
        draft.Value.FoldedThicknessPx.Should().Be(Settings.FoldedThicknessMin);
    }

    [Fact]
    public void Dispose_saves_pending_edits_then_stops_following_the_store()
    {
        var draft = NewDraft();
        draft.Edit(s => s with { Scale = 1.2 });

        draft.Dispose();

        _store.Current.Scale.Should().Be(1.2);
        _saves.Should().Be(1);

        var changed = 0;
        draft.Changed += () => changed++;
        _store.Save(_store.Current with { Edge = ScreenEdge.Top });
        draft.Edit(s => s with { Scale = 0.5 });
        _time.Advance(SettingsDraft.CommitDelay * 2);

        changed.Should().Be(0);
        _store.Current.Scale.Should().Be(1.2);
        draft.Dispose();
    }
}
