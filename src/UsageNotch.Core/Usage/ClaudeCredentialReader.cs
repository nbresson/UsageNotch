using System.Text.Json;

namespace UsageNotch.Core.Usage;

public sealed record ClaudeCredential(string AccessToken, bool IsExpired);

/// <summary>Lit le jeton OAuth que Claude Code conserve dans <c>~/.claude/.credentials.json</c>. Lecture seule, jamais de rafraîchissement.</summary>
public sealed class ClaudeCredentialReader(string claudeDirectory, TimeProvider time)
{
    private static readonly string[] FileNames = [".credentials.json", "credentials.json"];

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public string Directory { get; } = claudeDirectory;

    public ClaudeCredential? Read()
    {
        foreach (var name in FileNames)
        {
            var path = Path.Combine(Directory, name);
            string text;
            try
            {
                if (!File.Exists(path)) continue;
                text = File.ReadAllText(path);
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            var cred = ParseCredential(text);
            if (cred is not null) return cred;
        }
        return null;
    }

    private ClaudeCredential? ParseCredential(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var oauth = root.TryGetProperty("claudeAiOauth", out var nested) && nested.ValueKind == JsonValueKind.Object
                ? nested
                : root;

            if (!oauth.TryGetProperty("accessToken", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String) return null;
            var token = tokenEl.GetString();
            if (string.IsNullOrEmpty(token)) return null;

            var expired = false;
            if (oauth.TryGetProperty("expiresAt", out var expEl) && expEl.ValueKind == JsonValueKind.Number && expEl.TryGetDouble(out var ms))
            {
                expired = (long)ms <= time.GetUtcNow().ToUnixTimeMilliseconds();
            }
            return new ClaudeCredential(token, expired);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
