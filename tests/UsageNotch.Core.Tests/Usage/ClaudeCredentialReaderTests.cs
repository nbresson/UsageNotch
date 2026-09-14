using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ClaudeCredentialReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static ClaudeCredentialReader Reader(TempDir dir) =>
        new(dir.Path, new FakeTimeProvider(Now));

    [Fact]
    public void Returns_null_when_no_file_exists()
    {
        using var dir = new TempDir();
        Reader(dir).Read().Should().BeNull();
    }

    [Fact]
    public void Returns_null_on_invalid_json()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), "{ broken");
        Reader(dir).Read().Should().BeNull();
    }

    [Fact]
    public void Returns_null_when_access_token_is_missing()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), """{ "claudeAiOauth": { "refreshToken": "r" } }""");
        Reader(dir).Read().Should().BeNull();
    }

    [Fact]
    public void Reads_nested_claudeAiOauth_shape_and_expiry()
    {
        using var dir = new TempDir();
        var expiresAt = Now.AddHours(1).ToUnixTimeMilliseconds();
        File.WriteAllText(dir.File(".credentials.json"),
            $$"""{ "claudeAiOauth": { "accessToken": "sk-ant-abc", "expiresAt": {{expiresAt}} } }""");

        var cred = Reader(dir).Read();

        cred.Should().NotBeNull();
        cred!.AccessToken.Should().Be("sk-ant-abc");
        cred.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Flags_an_expired_token()
    {
        using var dir = new TempDir();
        var expiresAt = Now.AddMinutes(-1).ToUnixTimeMilliseconds();
        File.WriteAllText(dir.File(".credentials.json"),
            $$"""{ "claudeAiOauth": { "accessToken": "sk-ant-abc", "expiresAt": {{expiresAt}} } }""");

        Reader(dir).Read()!.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void Reads_flat_shape_without_wrapper()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), """{ "accessToken": "flat-token" }""");
        Reader(dir).Read()!.AccessToken.Should().Be("flat-token");
    }

    [Fact]
    public void Falls_back_to_credentials_json_without_dot()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("credentials.json"), """{ "claudeAiOauth": { "accessToken": "second" } }""");
        Reader(dir).Read()!.AccessToken.Should().Be("second");
    }

    [Fact]
    public void Default_directory_is_dot_claude_under_the_user_profile()
    {
        ClaudeCredentialReader.DefaultDirectory.Should().EndWith(".claude");
    }

    [Fact]
    public void ToString_redacts_the_access_token()
    {
        var cred = new ClaudeCredential("sk-ant-secret-token", IsExpired: false);

        var text = cred.ToString();

        text.Should().NotContain("sk-ant-secret-token");
        text.Should().Contain("AccessToken = ***");
    }
}
