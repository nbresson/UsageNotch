using System.Net;
using System.Text.RegularExpressions;

namespace UsageNotch.Core.Security;

/// <summary>
/// Désinfecte et normalise les messages d'erreurs et d'exceptions réseau
/// pour empêcher toute fuite de clés d'API, de jetons d'authentification
/// ou de paramètres d'URL sensibles dans l'interface utilisateur.
/// </summary>
public static partial class ExceptionSanitizer
{
    private const int MaxMessageLength = 160;

    [GeneratedRegex(@"sk-[a-zA-Z0-9_\-]{8,}", RegexOptions.Compiled)]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"(?i)bearer\s+[a-zA-Z0-9_\-\.]+", RegexOptions.Compiled)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(https?://[^\s\?]+)\?[^\s]+", RegexOptions.Compiled)]
    private static partial Regex UrlQueryRegex();

    [GeneratedRegex(@"(?i)(key|token|secret|password|auth)=[^&\s]+", RegexOptions.Compiled)]
    private static partial Regex QueryParamSecretRegex();

    /// <summary>
    /// Produit un message d'erreur utilisateur sain et sécurisé à partir d'une exception.
    /// </summary>
    public static string Sanitize(Exception? ex)
    {
        if (ex is null) return "Erreur inconnue";

        if (ex is OperationCanceledException or TimeoutException)
        {
            return "Délai d'attente dépassé";
        }

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return "Identifiants ou clé d'API invalides";
            }
            if (httpEx.StatusCode is >= HttpStatusCode.InternalServerError)
            {
                return $"Erreur temporaire du serveur distant ({(int)httpEx.StatusCode})";
            }
            if (httpEx.HttpRequestError is HttpRequestError.NameResolutionError
                or HttpRequestError.ConnectionError)
            {
                return "Impossible de joindre le serveur distant";
            }
        }

        return SanitizeMessage(ex.Message);
    }

    /// <summary>
    /// Masque les secrets potentiels dans une chaîne de message.
    /// </summary>
    public static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Erreur inconnue";

        var sanitized = ApiKeyRegex().Replace(message, "sk-***");
        sanitized = BearerTokenRegex().Replace(sanitized, "Bearer ***");
        sanitized = UrlQueryRegex().Replace(sanitized, "$1?...");
        sanitized = QueryParamSecretRegex().Replace(sanitized, "$1=***");

        sanitized = sanitized.Trim();
        if (sanitized.Length > MaxMessageLength)
        {
            sanitized = sanitized[..MaxMessageLength] + "…";
        }

        return sanitized;
    }
}
