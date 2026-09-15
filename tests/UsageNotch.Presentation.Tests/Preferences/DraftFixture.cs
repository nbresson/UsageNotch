using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Settings;
using UsageNotch.Presentation.Preferences;

namespace UsageNotch.Presentation.Tests.Preferences;

/// <summary>Un magasin de réglages sur disque temporaire, éventuellement prérempli, et son brouillon.</summary>
public sealed class DraftFixture : IDisposable
{
    public DraftFixture(Func<Settings, Settings>? initial = null)
    {
        Store = new SettingsStore(Dir.File("settings.json"), NullLogger<SettingsStore>.Instance, Time);
        Store.Load();
        if (initial is not null) Store.Save(initial(Store.Current));
        Draft = new SettingsDraft(Store, new ImmediateDispatcher(), Time);
    }

    public TempDir Dir { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    public SettingsStore Store { get; }
    public SettingsDraft Draft { get; }

    public void Dispose()
    {
        Draft.Dispose();
        Dir.Dispose();
    }
}
