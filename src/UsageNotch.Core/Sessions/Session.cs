namespace UsageNotch.Core.Sessions;

public sealed record Session(
    string Id,
    string Title,
    SessionState State,
    DateTimeOffset Started,
    TimeSpan Total,
    string LastAction,
    string AttentionMessage,
    string Prompt,
    string Model,
    int ParentPid,
    string Cwd,
    DateTimeOffset LastEvent);
