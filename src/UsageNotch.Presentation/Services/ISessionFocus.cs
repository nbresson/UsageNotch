namespace UsageNotch.Presentation.Services;

/// <summary>
/// Ramène au premier plan la fenêtre qui héberge la session. Faux si rien n'a été trouvé, ou si le processus parent a
/// démarré après la session (son numéro a été réutilisé par un autre programme).
/// </summary>
public interface ISessionFocus
{
    bool Focus(int? parentPid, DateTimeOffset sessionStarted);
}
