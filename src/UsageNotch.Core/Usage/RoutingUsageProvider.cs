using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Délègue dynamiquement au fournisseur d'usage actif désigné par <see cref="Settings.Settings.Provider"/>.
/// </summary>
public sealed class RoutingUsageProvider(
    SettingsStore settings,
    Func<string, IUsageProvider> resolver) : IUsageProvider
{
    private IUsageProvider Active => resolver(settings.Current.Provider);

    public string Id => Active.Id;

    public string DisplayName => Active.DisplayName;

    public IReadOnlyList<IReadOnlyList<string>> RingWindowIds => Active.RingWindowIds;

    public Task<FetchResult> FetchAsync(CancellationToken ct) => Active.FetchAsync(ct);
}
