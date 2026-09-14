namespace UsageNotch.Core.Usage;

public interface IUsageProvider
{
    /// <summary>Identifiant stable, ex. « claude ».</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>La fenêtre que l'anneau dessine. Absente de la réponse : la cellule montre un tiret.</summary>
    string HeadlineWindowId { get; }

    Task<FetchResult> FetchAsync(CancellationToken ct);
}
