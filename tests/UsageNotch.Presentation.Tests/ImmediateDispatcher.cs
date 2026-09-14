using UsageNotch.Presentation.Services;

namespace UsageNotch.Presentation.Tests;

public sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
