using System.Net;
using FluentAssertions;
using UsageNotch.Core.Security;

namespace UsageNotch.Core.Tests.Security;

public class ExceptionSanitizerTests
{
    [Fact]
    public void Sanitize_null_exception_returns_default_error()
    {
        ExceptionSanitizer.Sanitize(null).Should().Be("Erreur inconnue");
    }

    [Fact]
    public void Sanitize_timeout_or_canceled_returns_timeout_message()
    {
        ExceptionSanitizer.Sanitize(new OperationCanceledException()).Should().Be("Délai d'attente dépassé");
        ExceptionSanitizer.Sanitize(new TimeoutException()).Should().Be("Délai d'attente dépassé");
    }

    [Fact]
    public void Sanitize_http_unauthorized_returns_auth_error()
    {
        var ex = new HttpRequestException("401 error", null, HttpStatusCode.Unauthorized);
        ExceptionSanitizer.Sanitize(ex).Should().Be("Identifiants ou clé d'API invalides");
    }

    [Fact]
    public void Sanitize_http_500_returns_server_error()
    {
        var ex = new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);
        ExceptionSanitizer.Sanitize(ex).Should().Be("Erreur temporaire du serveur distant (500)");
    }

    [Fact]
    public void SanitizeMessage_masks_api_keys()
    {
        const string raw = "Erreur avec la clé sk-proj-1234567890abcdef12345 sur le serveur";
        var sanitized = ExceptionSanitizer.SanitizeMessage(raw);
        sanitized.Should().NotContain("1234567890abcdef12345");
        sanitized.Should().Contain("sk-***");
    }

    [Fact]
    public void SanitizeMessage_masks_bearer_tokens()
    {
        const string raw = "Invalid token Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.secret";
        var sanitized = ExceptionSanitizer.SanitizeMessage(raw);
        sanitized.Should().NotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");
        sanitized.Should().Contain("Bearer ***");
    }

    [Fact]
    public void SanitizeMessage_strips_url_query_parameters()
    {
        const string raw = "Failed call to https://api.openai.com/v1/costs?start_time=12345&secret=supersecret";
        var sanitized = ExceptionSanitizer.SanitizeMessage(raw);
        sanitized.Should().NotContain("supersecret");
        sanitized.Should().Contain("https://api.openai.com/v1/costs?...");
    }

    [Fact]
    public void SanitizeMessage_truncates_excessively_long_messages()
    {
        var raw = new string('a', 300);
        var sanitized = ExceptionSanitizer.SanitizeMessage(raw);
        sanitized.Length.Should().BeLessThanOrEqualTo(162);
        sanitized.Should().EndWith("…");
    }
}
