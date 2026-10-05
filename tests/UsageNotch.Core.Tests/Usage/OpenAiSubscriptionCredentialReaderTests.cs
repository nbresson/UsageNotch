using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.Core.Settings;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class OpenAiSubscriptionCredentialReaderTests
{
    private static SettingsStore CreateSettingsStore(TempDir dir, UsageNotch.Core.Settings.Settings? initial = null)
    {
        var store = new SettingsStore(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);
        if (initial != null) store.Save(initial);
        return store;
    }

    [Fact]
    public void Returns_null_when_no_token_in_settings_and_no_file()
    {
        using var dir = new TempDir();
        var settings = CreateSettingsStore(dir);
        var reader = new OpenAiSubscriptionCredentialReader(settings, codexHomeOverride: dir.Path);

        reader.Read().Should().BeNull();
    }

    [Fact]
    public void Returns_token_from_settings_with_priority()
    {
        using var dir = new TempDir();
        var initial = new UsageNotch.Core.Settings.Settings
        {
            OpenAiSessionToken = "manual-token-xyz",
            OpenAiAccountId = "acc-manual-123"
        };
        var settings = CreateSettingsStore(dir, initial);

        // Even if file exists, settings token has priority
        File.WriteAllText(dir.File("auth.json"), """{ "tokens": { "access_token": "file-token" } }""");

        var reader = new OpenAiSubscriptionCredentialReader(settings, codexHomeOverride: dir.Path);
        var cred = reader.Read();

        cred.Should().NotBeNull();
        cred!.AccessToken.Should().Be("manual-token-xyz");
        cred.AccountId.Should().Be("acc-manual-123");
    }

    [Fact]
    public void Reads_standard_codex_nested_tokens_format_from_file()
    {
        using var dir = new TempDir();
        var settings = CreateSettingsStore(dir);
        File.WriteAllText(dir.File("auth.json"), """
        {
            "auth_mode": "chatgpt",
            "OPENAI_API_KEY": null,
            "tokens": {
                "access_token": "oa-token-nested-456",
                "refresh_token": "ref-123",
                "account_id": "account-org-789"
            }
        }
        """);

        var reader = new OpenAiSubscriptionCredentialReader(settings, codexHomeOverride: dir.Path);
        var cred = reader.Read();

        cred.Should().NotBeNull();
        cred!.AccessToken.Should().Be("oa-token-nested-456");
        cred.AccountId.Should().Be("account-org-789");
    }

    [Fact]
    public void Reads_flat_format_from_file()
    {
        using var dir = new TempDir();
        var settings = CreateSettingsStore(dir);
        File.WriteAllText(dir.File("auth.json"), """
        {
            "access_token": "oa-flat-token",
            "account_id": "acc-flat-id"
        }
        """);

        var reader = new OpenAiSubscriptionCredentialReader(settings, codexHomeOverride: dir.Path);
        var cred = reader.Read();

        cred.Should().NotBeNull();
        cred!.AccessToken.Should().Be("oa-flat-token");
        cred.AccountId.Should().Be("acc-flat-id");
    }

    [Fact]
    public void Returns_null_when_file_is_invalid_json()
    {
        using var dir = new TempDir();
        var settings = CreateSettingsStore(dir);
        File.WriteAllText(dir.File("auth.json"), "{ broken json");

        var reader = new OpenAiSubscriptionCredentialReader(settings, codexHomeOverride: dir.Path);
        reader.Read().Should().BeNull();
    }

    [Fact]
    public void Returns_null_when_tokens_exist_but_access_token_is_missing()
    {
        using var dir = new TempDir();
        var settings = CreateSettingsStore(dir);
        File.WriteAllText(dir.File("auth.json"), """
        {
            "tokens": {
                "refresh_token": "only-refresh"
            }
        }
        """);

        var reader = new OpenAiSubscriptionCredentialReader(settings, codexHomeOverride: dir.Path);
        reader.Read().Should().BeNull();
    }
}
