using Andy.Engine.SweBench.Llm;
using FluentAssertions;
using Xunit;

namespace Andy.Engine.Tests.SweBench;

/// <summary>
/// IsTransient must not treat any message merely CONTAINING the digits "429" (a request id, a
/// token count) as retryable — that turned permanent errors into 6 backoff retries per call
/// (issue #43). Explicit status codes and the textual rate-limit hints still count.
/// </summary>
public class RateLimitPolicyTests
{
    [Theory]
    [InlineData("OpenRouter request failed (status 429): slow down")]
    [InlineData("Status: 429 Too Many Requests")]
    [InlineData("HTTP 429")]
    [InlineData("status code 500")]
    [InlineData("(status 503): upstream unavailable")]
    [InlineData("Too Many Requests")]
    [InlineData("provider rate limit hit, retry later")]
    [InlineData("The input does not contain any JSON tokens")]
    public void Transient_messages_are_retryable(string message) =>
        RateLimitPolicy.IsTransient(message).Should().BeTrue();

    [Theory]
    [InlineData("(status 400): request id req_429abc is malformed")]
    [InlineData("(status 401): invalid api key")]
    [InlineData("prompt is 4290 tokens which exceeds the model limit")]
    [InlineData("request 84293 rejected: content policy")]
    [InlineData("")]
    public void NonTransient_messages_fail_fast(string message) =>
        RateLimitPolicy.IsTransient(message).Should().BeFalse();

    [Fact]
    public void Explicit_status_wins_over_textual_hints()
    {
        // A parsed non-retryable status short-circuits, even if rate-limit words appear later.
        RateLimitPolicy.IsTransient("(status 400): you mentioned rate limit in your prompt")
            .Should().BeFalse();
    }
}
