namespace UsageNotch.Core.Sessions;

/// <summary>Ordonné par coût d'attention croissant : l'agrégat est le maximum.</summary>
public enum SessionState
{
    Idle,
    Done,
    Running,
    Attention,
}
