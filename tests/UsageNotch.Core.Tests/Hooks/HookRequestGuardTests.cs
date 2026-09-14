using FluentAssertions;
using UsageNotch.Core.Hooks;

namespace UsageNotch.Core.Tests.Hooks;

public class HookRequestGuardTests
{
    [Theory]
    [InlineData("http://127.0.0.1:48666")]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://localhost")]
    [InlineData("https://127.0.0.1:48666")]
    [InlineData("https://localhost:3000")]
    [InlineData("127.0.0.1:48666")]
    [InlineData("localhost")]
    [InlineData("http://[::1]:8080")]
    [InlineData("http://[::1]")]
    [InlineData("http://LOCALHOST:3000")]
    public void Loopback_origins_are_allowed(string origin) =>
        HookRequestGuard.IsAllowedOrigin(origin).Should().BeTrue();

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("http://attacker.com:8080")]
    [InlineData("https://evil-localhost.com")]
    [InlineData("https://localhost.attacker.com")]
    [InlineData("http://127.0.0.1.attacker.com")]
    [InlineData("http://attacker.com:127.0.0.1")]
    [InlineData("http://localhost@attacker.com")]
    [InlineData("http://attacker.com/localhost")]
    [InlineData("http://attacker.com?localhost")]
    [InlineData("http://attacker.com#localhost")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://localhost:abc")]
    [InlineData("http://localhost:70000")]
    [InlineData("http://localhost:0")]
    [InlineData("http://localhost:")]
    public void Untrusted_origins_are_rejected(string origin) =>
        HookRequestGuard.IsAllowedOrigin(origin).Should().BeFalse();

    private static KeyValuePair<string, string> H(string k, string v) => new(k, v);

    [Fact]
    public void A_native_hook_request_without_origin_is_allowed() =>
        HookRequestGuard.IsForbidden([H("Host", "127.0.0.1:48666"), H("Content-Type", "application/json")]).Should().BeFalse();

    [Fact]
    public void An_untrusted_origin_is_forbidden() =>
        HookRequestGuard.IsForbidden([H("Origin", "https://evil.com")]).Should().BeTrue();

    [Fact]
    public void Two_origin_headers_are_forbidden() =>
        HookRequestGuard.IsForbidden([H("Origin", "http://localhost:3000"), H("Origin", "http://127.0.0.1:48666")]).Should().BeTrue();

    [Fact]
    public void A_trusted_origin_is_allowed() =>
        HookRequestGuard.IsForbidden([H("origin", "http://localhost:3000")]).Should().BeFalse();

    [Fact]
    public void Cross_site_fetch_metadata_is_forbidden_even_with_a_local_origin() =>
        HookRequestGuard.IsForbidden([H("Origin", "http://localhost:3000"), H("Sec-Fetch-Site", "cross-site")]).Should().BeTrue();

    [Fact]
    public void Same_origin_fetch_metadata_is_allowed() =>
        HookRequestGuard.IsForbidden([H("Sec-Fetch-Site", "same-origin")]).Should().BeFalse();
}
