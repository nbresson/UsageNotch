using System.IO;
using Microsoft.Win32;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Lit HKCU\Software\Microsoft\Windows\DWM\AccentColor (DWORD au format ABGR).</summary>
public sealed class SystemAccentColor : IAccentColorSource
{
    public string? AccentHex
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key?.GetValue("AccentColor") is not int abgr) return null;
                var v = unchecked((uint)abgr);
                return $"#{v & 0xFF:X2}{(v >> 8) & 0xFF:X2}{(v >> 16) & 0xFF:X2}";
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }
    }
}
