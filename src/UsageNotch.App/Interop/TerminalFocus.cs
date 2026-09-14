using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Provisoire : le retour au terminal est implémenté en Task 15.</summary>
public sealed class TerminalFocus : ISessionFocus
{
    public bool Focus(int? parentPid) => false;
}
