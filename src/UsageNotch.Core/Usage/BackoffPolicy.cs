namespace UsageNotch.Core.Usage;

/// <summary>60 s × 2^n, plafonné à 15 min ; un Retry-After ne fait que relever le plancher.</summary>
public sealed class BackoffPolicy
{
    public static readonly TimeSpan Base = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Cap = TimeSpan.FromMinutes(15);

    public int ConsecutiveFailures { get; private set; }

    public TimeSpan Next(TimeSpan retryAfterFloor)
    {
        var exponent = Math.Min(ConsecutiveFailures, 4);
        ConsecutiveFailures++;
        var wait = TimeSpan.FromTicks(Base.Ticks * (1L << exponent));
        if (wait > Cap) wait = Cap;
        return wait > retryAfterFloor ? wait : retryAfterFloor;
    }

    public void Reset() => ConsecutiveFailures = 0;
}
