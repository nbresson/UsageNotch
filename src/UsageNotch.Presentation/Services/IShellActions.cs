namespace UsageNotch.Presentation.Services;

/// <summary>Ouvertures de dossiers et de fichiers, et diagnostic, pour les pages de réglages.</summary>
public interface IShellActions
{
    void OpenFolder(string path);

    void OpenFile(string path);

    /// <summary>Écrit le diagnostic dans le dossier des journaux et l'ouvre. Null si tout s'est bien passé, sinon un message.</summary>
    string? RunDoctor();
}
