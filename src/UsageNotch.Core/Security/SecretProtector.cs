using System.Security.Cryptography;
using System.Text;

namespace UsageNotch.Core.Security;

/// <summary>
/// Chiffre et déchiffre les données sensibles (clés d'API, jetons de session)
/// via l'API Windows DPAPI (portée de l'utilisateur courant).
/// Assure une rétrocompatibilité totale : les valeurs en clair existantes sont lues
/// sans erreur et rechiffrées au prochain enregistrement.
/// </summary>
public static class SecretProtector
{
    public const string DpapiPrefix = "dpapi:";

    public static string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        if (plainText.StartsWith(DpapiPrefix, StringComparison.Ordinal)) return plainText;
        if (!OperatingSystem.IsWindows()) return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return DpapiPrefix + Convert.ToBase64String(cipherBytes);
        }
        catch
        {
            return plainText;
        }
    }

    public static string Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        if (!cipherText.StartsWith(DpapiPrefix, StringComparison.Ordinal)) return cipherText;
        if (!OperatingSystem.IsWindows()) return cipherText[DpapiPrefix.Length..];

        try
        {
            var base64 = cipherText[DpapiPrefix.Length..];
            var cipherBytes = Convert.FromBase64String(base64);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return "";
        }
    }
}
