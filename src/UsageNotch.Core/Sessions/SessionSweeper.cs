using Microsoft.Extensions.Hosting;

namespace UsageNotch.Core.Sessions;

/// <summary>Balayage des sessions obsolètes toutes les 30 s.</summary>
public sealed class SessionSweeper(SessionStore store, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, time, stoppingToken);
            store.Sweep();
        }
    }
}
