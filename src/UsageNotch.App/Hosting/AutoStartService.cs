using System.IO;
using System.Security;
using UsageNotch.App.Interop;
using UsageNotch.Presentation;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Hosting;

/// <summary>« Démarrer avec Windows ». En démo, la vraie valeur Run de HKCU n'est jamais lue ni modifiée.</summary>
public sealed class AutoStartService(AppArguments args) : IAutoStart
{
    public bool IsAvailable => !args.Demo;

    public bool IsEnabled()
    {
        if (!IsAvailable) return false;
        try
        {
            return AutoStart.IsEnabled();
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public string? TrySet(bool enabled)
    {
        if (!IsAvailable) return "Démarrer avec Windows est indisponible en mode démo.";
        try
        {
            AutoStart.Set(enabled, Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "UsageNotch.App.exe"));
            return null;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return "Impossible de modifier le démarrage avec Windows : " + e.Message;
        }
    }
}
