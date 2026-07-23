using System.Runtime.CompilerServices;
using Andy.Engine.SweBench.Llm;
using Andy.Model.Llm;
using Andy.Model.Model;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andy.Engine.Tests.SweBench;

/// <summary>
/// StreamCompleteAsync previously bypassed retry entirely, so the day SimpleAgent switched to
/// streaming it would silently lose rate-limit protection. Now transient failures are retried
/// until the first chunk reaches the consumer; afterwards a restart would duplicate output, so
/// failures propagate.
/// </summary>
public class RateLimitingLlmProviderStreamTests
{
    private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

    private static LlmRequest Request() => new()
    {
        Messages = new List<Message> { new() { Role = Role.User, Content = "hi" } },
    };

    private static LlmStreamResponse Chunk(string text) => new()
    {
        Delta = new Message { Role = Role.Assistant, Content = text },
    };

    private static async IAsyncEnumerable<LlmStreamResponse> Yield(
        params LlmStreamResponse[] chunks)
    {
        foreach (var c in chunks)
        {
            await Task.Yield();
            yield return c;
        }
    }

    private static async IAsyncEnumerable<LlmStreamResponse> ThrowImmediately(Exception ex)
    {
        await Task.Yield();
        throw ex;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<LlmStreamResponse> YieldThenThrow(
        LlmStreamResponse first, Exception ex)
    {
        await Task.Yield();
        yield return first;
        throw ex;
    }

    [Fact]
    public async Task TransientThrow_BeforeFirstChunk_IsRetried()
    {
        var inner = new Mock<ILlmProvider>();
        var attempts = 0;
        inner.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return attempts <= 2
                    ? ThrowImmediately(new InvalidOperationException("(status 429): slow down"))
                    : Yield(Chunk("ok"), new LlmStreamResponse { IsComplete = true, FinishReason = "stop" });
            });

        var provider = new RateLimitingLlmProvider(inner.Object, new RateLimitPolicy(), delay: NoDelay);

        var chunks = new List<LlmStreamResponse>();
        await foreach (var c in provider.StreamCompleteAsync(Request()))
            chunks.Add(c);

        attempts.Should().Be(3);
        chunks.Should().HaveCount(2);
        chunks[0].TextDelta.Should().Be("ok");
    }

    [Fact]
    public async Task TransientErrorChunk_BeforeAnyOutput_IsRetried()
    {
        var inner = new Mock<ILlmProvider>();
        var attempts = 0;
        inner.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return attempts == 1
                    ? Yield(new LlmStreamResponse
                    {
                        IsComplete = true,
                        Error = "OpenRouter request failed (status 503): upstream",
                    })
                    : Yield(Chunk("recovered"), new LlmStreamResponse { IsComplete = true, FinishReason = "stop" });
            });

        var provider = new RateLimitingLlmProvider(inner.Object, new RateLimitPolicy(), delay: NoDelay);

        var chunks = new List<LlmStreamResponse>();
        await foreach (var c in provider.StreamCompleteAsync(Request()))
            chunks.Add(c);

        attempts.Should().Be(2);
        chunks[0].TextDelta.Should().Be("recovered");
    }

    [Fact]
    public async Task FailureAfterFirstChunk_Propagates_WithoutRetry()
    {
        var inner = new Mock<ILlmProvider>();
        var attempts = 0;
        inner.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return YieldThenThrow(Chunk("partial"), new InvalidOperationException("(status 429): mid-stream"));
            });

        var provider = new RateLimitingLlmProvider(inner.Object, new RateLimitPolicy(), delay: NoDelay);

        var received = new List<LlmStreamResponse>();
        var act = async () =>
        {
            await foreach (var c in provider.StreamCompleteAsync(Request()))
                received.Add(c);
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(1, "a stream that already yielded output must not be restarted");
        received.Should().ContainSingle().Which.TextDelta.Should().Be("partial");
    }

    [Fact]
    public async Task NonTransientThrow_Propagates_Immediately()
    {
        var inner = new Mock<ILlmProvider>();
        var attempts = 0;
        inner.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return ThrowImmediately(new InvalidOperationException("(status 401): bad key"));
            });

        var provider = new RateLimitingLlmProvider(inner.Object, new RateLimitPolicy(), delay: NoDelay);

        var act = async () =>
        {
            await foreach (var _ in provider.StreamCompleteAsync(Request())) { }
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExhaustedRetries_ThrowRateLimitExhausted()
    {
        var policy = new RateLimitPolicy { MaxRetries = 2 };
        var inner = new Mock<ILlmProvider>();
        var attempts = 0;
        inner.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return ThrowImmediately(new InvalidOperationException("(status 429): always"));
            });

        var provider = new RateLimitingLlmProvider(inner.Object, policy, delay: NoDelay);

        var act = async () =>
        {
            await foreach (var _ in provider.StreamCompleteAsync(Request())) { }
        };

        await act.Should().ThrowAsync<RateLimitExhaustedException>();
        attempts.Should().Be(3, "initial attempt + MaxRetries");
    }
}
