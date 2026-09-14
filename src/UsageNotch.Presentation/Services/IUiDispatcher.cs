namespace UsageNotch.Presentation.Services;

/// <summary>Exécute une action sur le thread de l'interface. L'App fournit une implémentation WPF.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
