using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly SettingsStore _store;

    public SettingsViewModelTests()
    {
        _store = new SettingsStore(_dir.File("settings.json"), NullLogger<SettingsStore>.Instance, _time);
        _store.Load();
    }

    public void Dispose() => _dir.Dispose();

    private SettingsViewModel Create(FakeMonitors? monitors = null) => SettingsViewModel.Create(
        _store,
        new ImmediateDispatcher(),
        _time,
        new FakeAccent(),
        new FakeColorPicker(),
        monitors ?? new FakeMonitors(),
        new FakeSound(),
        new FakeAutoStart(),
        new FakeHooks(),
        new FakeShell(),
        new SettingsEnvironment("0.3.0", false, @"C:\data", @"C:\data\logs", @"C:\data\settings.json", 48666, true));

    [Fact]
    public void The_five_spec_pages_are_listed_in_order_and_the_first_is_selected()
    {
        using var vm = Create();

        vm.Pages.Select(p => p.Title).Should().Equal("Apparence", "Position", "Comportement", "Claude Code", "À propos");
        vm.Pages.Select(p => p.Kind).Should().Equal(
            SettingsPageKind.Appearance, SettingsPageKind.Position, SettingsPageKind.Behavior, SettingsPageKind.ClaudeCode, SettingsPageKind.About);
        vm.Pages.Select(p => p.ViewModel).Should().Equal(vm.Appearance, vm.Position, vm.Behavior, vm.ClaudeCode, vm.About);
        vm.Pages[3].ToString().Should().Be("Claude Code");
        vm.SelectedPage.Should().Be(vm.Pages[0]);
    }

    [Fact]
    public void A_page_can_be_selected_by_kind_and_a_null_selection_is_ignored()
    {
        using var vm = Create();

        vm.Select(SettingsPageKind.ClaudeCode);
        vm.SelectedPage.Kind.Should().Be(SettingsPageKind.ClaudeCode);

        vm.SelectedPage = null!;
        vm.SelectedPage.Kind.Should().Be(SettingsPageKind.ClaudeCode);
    }

    [Fact]
    public void The_preview_follows_every_edit()
    {
        using var vm = Create();
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        vm.Appearance.Preset = ThemePreset.Monochrome;
        vm.Position.Edge = ScreenEdge.Bottom;

        vm.Preview.Theme.Should().Be(Theme.Monochrome);
        vm.Preview.Edge.Should().Be(ScreenEdge.Bottom);
        names.Should().Contain(nameof(SettingsViewModel.Preview));
    }

    [Fact]
    public void Flush_saves_at_once()
    {
        using var vm = Create();

        vm.Appearance.Scale = 1.3;
        vm.Flush();

        _store.Current.Scale.Should().Be(1.3);
    }

    [Fact]
    public void Dispose_saves_pending_edits_and_detaches_the_pages()
    {
        var vm = Create();
        vm.Appearance.Scale = 1.3;
        var pageNotifications = 0;
        vm.Appearance.PropertyChanged += (_, _) => pageNotifications++;

        vm.Dispose();
        _store.Save(_store.Current with { Edge = ScreenEdge.Top });

        _store.Current.Scale.Should().Be(1.3);
        pageNotifications.Should().Be(0);
    }
}
