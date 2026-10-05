using System.Text.Json;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>Identifiants d'accès pour l'abonnement OpenAI / ChatGPT / Codex.</summary>
public sealed record OpenAiSubscriptionCredentials(string AccessToken, string? AccountId);

/// <summary>
/// Lit les identifiants d'abonnement OpenAI configurés dans les réglages (token manuel),
/// ou depuis le fichier local d'authentification Codex (~/.codex/auth.json ou $CODEX_HOME/auth.json).
/// </summary>
public sealed class OpenAiSubscriptionCredentialReader(
    SettingsStore? settings = null,
    string? codexHomeOverride = null)
{
    public OpenAiSubscriptionCredentials? Read()
    {
        var manualToken = settings?.Current.OpenAiSessionToken?.Trim();
        if (!string.IsNullOrEmpty(manualToken))
        {
            var accountId = settings?.Current.OpenAiAccountId?.Trim();
            return new OpenAiSubscriptionCredentials(manualToken, string.IsNullOrEmpty(accountId) ? null : accountId);
        }

        string authFilePath;
        if (!string.IsNullOrWhiteSpace(codexHomeOverride))
        {
            authFilePath = Path.Combine(codexHomeOverride, "auth.json");
        }
        else
        {
            var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!string.IsNullOrWhiteSpace(codexHome))
            {
                authFilePath = Path.Combine(codexHome, "auth.json");
            }
            else
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                authFilePath = Path.Combine(userProfile, ".codex", "auth.json");
            }
        }

        return ReadFromFile(authFilePath);
    }

    public static OpenAiSubscriptionCredentials? ReadFromFile(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return ParseCredentials(json);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (Exception) { return null; }
    }

    public static OpenAiSubscriptionCredentials? ParseCredentials(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? token = null;
            string? accountId = null;

            // Format standard Codex:
            // { "auth_mode": "chatgpt", "tokens": { "access_token": "...", "account_id": "..." } }
            if (root.TryGetProperty("tokens", out var tokensEl) && tokensEl.ValueKind == JsonValueKind.Object)
            {
                if (tokensEl.TryGetProperty("access_token", out var tokEl) && tokEl.ValueKind == JsonValueKind.String)
                {
                    token = tokEl.GetString();
                }
                if (tokensEl.TryGetProperty("account_id", out var accEl) && accEl.ValueKind == JsonValueKind.String)
                {
                    accountId = accEl.GetString();
                }
            }

            // Format plat:
            // { "access_token": "...", "account_id": "..." }
            if (string.IsNullOrEmpty(token) && root.TryGetProperty("access_token", out var flatTokEl) && flatTokEl.ValueKind == JsonValueKind.String)
            {
                token = flatTokEl.GetString();
            }
            if (string.IsNullOrEmpty(accountId) && root.TryGetProperty("account_id", out var flatAccEl) && flatAccEl.ValueKind == JsonValueKind.String)
            {
                accountId = flatAccEl.GetString();
            }

            token = token?.Trim();
            accountId = accountId?.Trim();

            if (string.IsNullOrEmpty(token)) return null;

            return new OpenAiSubscriptionCredentials(token, string.IsNullOrEmpty(accountId) ? null : accountId);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
