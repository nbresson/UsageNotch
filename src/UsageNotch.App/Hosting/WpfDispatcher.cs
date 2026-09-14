using System.Windows.Threading;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

public sealed class WpfDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action)
    {
        if (dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(action);
    }
}
