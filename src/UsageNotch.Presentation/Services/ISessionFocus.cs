namespace UsageNotch.Presentation.Services;

/// <summary>Ramène au premier plan la fenêtre qui héberge la session. Faux si rien n'a été trouvé.</summary>
public interface ISessionFocus
{
    bool Focus(int? parentPid);
}
