namespace UsageNotch.Presentation.Services;

/// <summary>« Démarrer avec Windows » (valeur Run de HKCU).</summary>
public interface IAutoStart
{
    /// <summary>Faux en mode démo : la vraie valeur Run ne doit jamais être lue ni modifiée.</summary>
    bool IsAvailable { get; }

    bool IsEnabled();

    /// <summary>Null si la modification a réussi, sinon un message d'erreur en français.</summary>
    string? TrySet(bool enabled);
}
