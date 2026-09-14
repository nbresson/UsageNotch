using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Sessions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;
using UsageNotch.Presentation.Behavior;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.Presentation.Tests.ViewModels;

public sealed class NotchViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeProvider : IUsageProvider
    {
        public string Id => "claude";
        public string DisplayName => "Claude";
        public string HeadlineWindowId => "session";
        public Task<FetchResult> FetchAsync(CancellationToken ct) => Task.FromResult<FetchResult>(new FetchResult.Failed("unused"));
    }

    private sealed class FakeFocus : ISessionFocus
    {
        public List<int?> Calls { get; } = [];
        public bool Focus(int? parentPid) { Calls.Add(parentPid); return true; }
    }

    private sealed class FakeSound : ISoundPlayer
    {
        public List<string> Played { get; } = [];
        public void Play(string soundName) => Played.Add(soundName);
    }

    private sealed class FakeAccent : IAccentColorSource
    {
        public string? AccentHex { get; set; }
    }

    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly UsageStore _usage;
    private readonly SessionStore _sessions;
    private readonly SettingsStore _settings;
    private readonly HoverController _hover;
    private readonly FakeFocus _focus = new();
    private readonly FakeSound _sound = new();
    private readonly FakeAccent _accent = new();
    private int _refreshes;
    private readonly NotchViewModel _vm;

    public NotchViewModelTests()
    {
        _usage = new UsageStore(_dir.File("usage.json"), _time, NullLogger<UsageStore>.Instance);
        _sessions = new SessionStore(_time);
        _settings = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _settings.Load();
        _hover = new HoverController(_time);
        _vm = new NotchViewModel(_usage, _sessions, _settings, new FakeProvider(), () => _refreshes++, _hover,
            new ImmediateDispatcher(), _focus, _sound, _accent, _time, TimeZoneInfo.Utc);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _hover.Dispose();
        _dir.Dispose();
    }

    private static HookEvent Ev(string kind, string id = "s-1", int ppid = 4242) =>
        new(kind, id, ppid, @"C:\src\proj", "", "", "", "", "");

    private static LimitWindow Session(double used, DateTimeOffset resets) => new("session", "Session en cours", used, resets);

    [Fact]
    public void The_initial_state_waits_for_the_first_reading()
    {
        _vm.Cell.PercentText.Should().Be("…");
        _vm.TrayText.Should().Be("UsageNotch — Claude …");
        _vm.Card.Note.Should().Be("En attente de la première lecture…");
        _vm.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_usage_reading_updates_the_cell_card_and_tray_text()
    {
        var raised = new List<string?>();
        ((INotifyPropertyChanged)_vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _usage.Apply(new FetchResult.Success([Session(0.73, Start.AddMinutes(51))]));

        _vm.Cell.PercentText.Should().Be("73" + FrenchText.Nbsp + "%");
        _vm.Card.Windows.Should().ContainSingle();
        _vm.TrayText.Should().Be("UsageNotch — Claude 73" + FrenchText.Nbsp + "%");
        raised.Should().Contain(new[] { nameof(NotchViewModel.Cell), nameof(NotchViewModel.Card), nameof(NotchViewModel.TrayText) });
    }

    [Fact]
    public void A_session_finishing_peeks_the_card_and_plays_the_done_sound()
    {
        _sessions.Apply(Ev(HookEvent.Running));
        _vm.CardVisible.Should().BeFalse();

        _sessions.Apply(Ev(HookEvent.Done));

        _vm.CardVisible.Should().BeTrue();
        _vm.Unfolded.Should().BeTrue();
        _sound.Played.Should().Equal("Asterisk");
        _vm.Card.Sessions.Should().ContainSingle(r => r.SessionId == "s-1");
    }

    [Fact]
    public void A_session_waiting_plays_the_attention_sound()
    {
        _sessions.Apply(Ev(HookEvent.Running));
        _sessions.Apply(Ev(HookEvent.Attention));
        _sound.Played.Should().Equal("Exclamation");
    }

    [Fact]
    public void Auto_open_and_sound_can_be_switched_off()
    {
        _settings.Save(_settings.Current with { AutoOpenCard = false, SoundEnabled = false });
        _sessions.Apply(Ev(HookEvent.Running));
        _sessions.Apply(Ev(HookEvent.Done));
        _vm.CardVisible.Should().BeFalse();
        _sound.Played.Should().BeEmpty();
    }

    [Fact]
    public void The_peek_closes_after_five_seconds()
    {
        _sessions.Apply(Ev(HookEvent.Running));
        _sessions.Apply(Ev(HookEvent.Done));
        _time.Advance(HoverController.PeekDuration + HoverController.CloseDelay);
        _vm.CardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_settings_change_applies_the_new_theme_and_cell_content()
    {
        _settings.Save(_settings.Current with { ThemePreset = ThemePreset.Monochrome, CellContent = CellContent.RingOnly });
        _vm.Theme.Should().Be(Theme.Monochrome);
        _vm.Cell.TrackColor.Should().Be(Theme.Monochrome.RingTrack);
        _vm.Cell.ShowPercent.Should().BeFalse();
        _vm.Settings.CellContent.Should().Be(CellContent.RingOnly);
    }

    [Fact]
    public void The_system_accent_preset_uses_the_accent_colour()
    {
        _accent.AccentHex = "#0078D4";
        _settings.Save(_settings.Current with { ThemePreset = ThemePreset.SystemAccent });
        _vm.Theme.LevelAmple.Should().Be("#0078D4");
    }

    [Fact]
    public void Commands_refresh_lock_and_peek()
    {
        _vm.RefreshCommand.Execute(null);
        _refreshes.Should().Be(1);

        _vm.ToggleLockCommand.Execute(null);
        _vm.Locked.Should().BeTrue();
        _vm.CardVisible.Should().BeTrue();
        _vm.ToggleLockCommand.Execute(null);
        _vm.Locked.Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(1));
        _vm.PeekCommand.Execute(null);
        _vm.CardVisible.Should().BeTrue();
    }

    [Fact]
    public void Session_commands_dismiss_and_focus_with_the_parent_pid()
    {
        _sessions.Apply(Ev(HookEvent.Running, "s-9", ppid: 777));

        _vm.FocusSessionCommand.Execute("s-9");
        _focus.Calls.Should().Equal(777);

        _vm.DismissSessionCommand.Execute("s-9");
        _sessions.Snapshot().Should().BeEmpty();
        _vm.Card.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void Pointer_methods_drive_the_hover_state()
    {
        _vm.PointerEnteredPill();
        _vm.CardVisible.Should().BeTrue();
        _vm.PointerLeftPill();
        _vm.PointerEnteredCard();
        _time.Advance(TimeSpan.FromSeconds(1));
        _vm.CardVisible.Should().BeTrue();
        _vm.PointerLeftCard();
        _time.Advance(HoverController.FoldDelay);
        _vm.CardVisible.Should().BeFalse();
        _vm.Unfolded.Should().BeFalse();
    }

    [Fact]
    public void The_clock_refreshes_relative_reset_texts()
    {
        _usage.Apply(new FetchResult.Success([Session(0.2, Start.AddMinutes(61))]));
        _vm.Card.Windows[0].ResetText.Should().Be("Réinitialisation à 13:01");

        _time.Advance(TimeSpan.FromMinutes(2));

        _vm.Card.Windows[0].ResetText.Should().Be("Réinitialisation dans 59 min");
    }

    [Fact]
    public void After_dispose_the_view_model_ignores_the_stores()
    {
        _vm.Dispose();
        _usage.Apply(new FetchResult.Success([Session(0.5, Start.AddHours(1))]));
        _vm.Cell.PercentText.Should().Be("…");
    }
}
