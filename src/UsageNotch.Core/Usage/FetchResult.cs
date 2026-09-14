namespace UsageNotch.Core.Usage;

/// <summary>Résultat brut d'un appel fournisseur. Le magasin en fait un snapshot.</summary>
public abstract record FetchResult
{
    private FetchResult() { }

    public sealed record Success(IReadOnlyList<LimitWindow> Windows) : FetchResult;
    public sealed record NeedsAuth(string Note) : FetchResult;
    public sealed record RateLimited(TimeSpan RetryAfter) : FetchResult;
    public sealed record Failed(string Note) : FetchResult;
}
