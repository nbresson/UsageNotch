namespace UsageNotch.Core.Usage;

/// <summary>
/// Registre centralisé des fournisseurs d'usage disponibles.
/// Découple la découverte et la résolution des implémentations concrètes de l'hôte d'application.
/// </summary>
public interface IUsageProviderRegistry
{
    /// <summary>Récupère un fournisseur par son identifiant unique (« claude », « antigravity », « openai »).</summary>
    IUsageProvider? GetProvider(string id);

    /// <summary>Tous les fournisseurs enregistrés dans le système.</summary>
    IReadOnlyList<IUsageProvider> All { get; }
}

/// <summary>
/// Implémentation en mémoire indexée par identifiant insensible à la casse.
/// </summary>
public sealed class UsageProviderRegistry : IUsageProviderRegistry
{
    private readonly Dictionary<string, IUsageProvider> _providers;

    public UsageProviderRegistry(IEnumerable<IUsageProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
        All = _providers.Values.ToList();
    }

    public IUsageProvider? GetProvider(string id) =>
        _providers.TryGetValue(id, out var provider) ? provider : null;

    public IReadOnlyList<IUsageProvider> All { get; }
}
