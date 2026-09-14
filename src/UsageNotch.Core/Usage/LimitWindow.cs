namespace UsageNotch.Core.Usage;

/// <summary>Une fenêtre de limite : « session », « weekly_all »… avec sa fraction utilisée (0 à 1).</summary>
public sealed record LimitWindow(string Id, string Label, double UsedFraction, DateTimeOffset ResetsAt);
