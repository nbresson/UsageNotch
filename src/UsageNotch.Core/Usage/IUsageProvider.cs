namespace UsageNotch.Core.Usage;

public interface IUsageProvider
{
    /// <summary>Identifiant stable, ex. « claude ».</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Les fenêtres des trois anneaux, de l'extérieur à l'intérieur, chacune par son groupe d'alias.</summary>
    IReadOnlyList<IReadOnlyList<string>> RingWindowIds { get; }

    Task<FetchResult> FetchAsync(CancellationToken ct);
}
