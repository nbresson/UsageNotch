using Microsoft.Extensions.Logging;
using UsageNotch.Core.Hooks;
using UsageNotch.Presentation.Services;
using UsageNotch.Presentation.ViewModels;

namespace UsageNotch.App.Hosting;

/// <summary>Crée et relie tout ce qui s'affiche. Doit être démarrée avant host.StartAsync (contrat du Plan 1).</summary>
public sealed class NotchShell(
    NotchViewModel viewModel,
    HookListener listener,
    IUiDispatcher ui,
    ILogger<NotchShell> logger) : IDisposable
{
    private Action? _onOpenSettings;

    public void Start()
    {
        // Plan 2 : une seconde instance lancée à la main montre la carte ; le Plan 3 ouvrira la fenêtre de réglages.
        _onOpenSettings = () => ui.Post(() => viewModel.PeekCommand.Execute(null));
        listener.OpenSettingsRequested += _onOpenSettings;
        logger.LogInformation("Coquille démarrée");
    }

    public void Dispose()
    {
        if (_onOpenSettings is not null) listener.OpenSettingsRequested -= _onOpenSettings;
        viewModel.Dispose();
    }
}
