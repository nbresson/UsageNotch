namespace UsageNotch.Core.Usage;

public sealed record UsageSnapshot(
    SnapshotStatus Status,
    IReadOnlyList<LimitWindow> Windows,
    DateTimeOffset FetchedAt,
    string Note,
    DateTimeOffset? BackoffUntil)
{
    public static UsageSnapshot Empty { get; } = new(SnapshotStatus.Error, [], DateTimeOffset.MinValue, "", null);

    public LimitWindow? Window(string id) => Windows.FirstOrDefault(w => w.Id == id);
}
