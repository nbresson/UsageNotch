namespace UsageNotch.Core.Sessions;

/// <summary>Un événement relayé par UsageNotch.Hook. Tous les champs texte sont vides plutôt que null quand ils manquent.</summary>
public sealed record HookEvent(
    string Kind,
    string SessionId,
    int ParentPid,
    string Cwd,
    string Prompt,
    string Message,
    string ToolName,
    string ToolCommand,
    string Model)
{
    public const string SessionStart = "session_start";
    public const string Running = "running";
    public const string Attention = "attention";
    public const string Done = "done";
    public const string SessionEnd = "session_end";

    /// <summary>Vrai pour l'un des cinq types d'événement connus (comparaison ordinale exacte).</summary>
    public static bool IsKnownKind(string kind) =>
        kind is SessionStart or Running or Attention or Done or SessionEnd;
}
