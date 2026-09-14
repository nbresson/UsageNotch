namespace UsageNotch.Core.Usage;

public enum SnapshotStatus
{
    Ok,
    Stale,
    NeedsAuth,
    Backoff,
    Error,
    Absent,
}
