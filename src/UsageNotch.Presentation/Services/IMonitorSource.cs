using UsageNotch.Core.Placement;

namespace UsageNotch.Presentation.Services;

/// <summary>Les écrans connectés, en pixels physiques.</summary>
public interface IMonitorSource
{
    IReadOnlyList<MonitorInfo> GetMonitors();
}
