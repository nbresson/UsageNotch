namespace UsageNotch.Core.Usage;

/// <summary>Ce que le planificateur a besoin de savoir des sessions : y en a-t-il une qui travaille ou attend ?</summary>
public interface ISessionActivity
{
    bool HasActiveSession { get; }
}
