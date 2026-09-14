namespace UsageNotch.Core.Placement;

/// <summary>Un écran tel que l'App l'énumère. <paramref name="Scale"/> : 1.0 = 100 %, 1.5 = 150 %.</summary>
public sealed record MonitorInfo(string DeviceId, bool IsPrimary, PixelRect Bounds, PixelRect WorkArea, double Scale);
