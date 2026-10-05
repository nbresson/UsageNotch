using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Usage;

/// <summary>Lit la clé d'API OpenAI configurée dans les réglages, ou depuis la variable d'environnement <c>OPENAI_API_KEY</c>.</summary>
public sealed class OpenAiCredentialReader(SettingsStore? settings = null)
{
    public string? Read()
    {
        var keyFromSettings = settings?.Current.OpenAiApiKey?.Trim();
        if (!string.IsNullOrEmpty(keyFromSettings))
        {
            return keyFromSettings;
        }

        var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim();
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }

        var defaultFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openai", "api_key");
        if (File.Exists(defaultFile))
        {
            try
            {
                var content = File.ReadAllText(defaultFile).Trim();
                if (!string.IsNullOrEmpty(content)) return content;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return null;
    }
}
