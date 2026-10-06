using System.Text.Json;
using FluentAssertions;
using UsageNotch.Core.Security;
using CoreSettings = UsageNotch.Core.Settings.Settings;

namespace UsageNotch.Core.Tests.Security;

public class SecretProtectorTests
{
    [Fact]
    public void Protect_empty_or_null_returns_empty()
    {
        SecretProtector.Protect("").Should().BeEmpty();
        SecretProtector.Protect(null).Should().BeEmpty();
    }

    [Fact]
    public void Unprotect_empty_or_null_returns_empty()
    {
        SecretProtector.Unprotect("").Should().BeEmpty();
        SecretProtector.Unprotect(null).Should().BeEmpty();
    }

    [Fact]
    public void Unprotect_legacy_plaintext_returns_plaintext_directly()
    {
        const string legacyKey = "sk-proj-123456789abcdef";
        SecretProtector.Unprotect(legacyKey).Should().Be(legacyKey);
    }

    [Fact]
    public void Protect_and_unprotect_roundtrip_preserves_secret()
    {
        const string secret = "sk-live-secret-token-key-998877";
        var protectedValue = SecretProtector.Protect(secret);

        if (OperatingSystem.IsWindows())
        {
            protectedValue.Should().StartWith(SecretProtector.DpapiPrefix);
            protectedValue.Should().NotBe(secret);
        }

        var recovered = SecretProtector.Unprotect(protectedValue);
        recovered.Should().Be(secret);
    }

    [Fact]
    public void Settings_json_serialization_encrypts_secrets_on_disk()
    {
        var settings = new CoreSettings
        {
            OpenAiApiKey = "sk-test-secret-key",
            OpenAiSessionToken = "session-jwt-token-abc",
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = false });

        if (OperatingSystem.IsWindows())
        {
            json.Should().NotContain("sk-test-secret-key");
            json.Should().NotContain("session-jwt-token-abc");
            json.Should().Contain(SecretProtector.DpapiPrefix);
        }

        var deserialized = JsonSerializer.Deserialize<CoreSettings>(json);
        deserialized.Should().NotBeNull();
        deserialized!.OpenAiApiKey.Should().Be("sk-test-secret-key");
        deserialized.OpenAiSessionToken.Should().Be("session-jwt-token-abc");
    }

    [Fact]
    public void Settings_deserializes_legacy_plaintext_without_error()
    {
        const string legacyJson = """
        {
            "openAiApiKey": "sk-legacy-plain-key",
            "openAiSessionToken": "session-plain-token"
        }
        """;

        var settings = JsonSerializer.Deserialize<CoreSettings>(legacyJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        settings.Should().NotBeNull();
        settings!.OpenAiApiKey.Should().Be("sk-legacy-plain-key");
        settings.OpenAiSessionToken.Should().Be("session-plain-token");
    }
}
