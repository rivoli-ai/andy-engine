using Andy.Engine.SweBench.Llm;
using Andy.Model.Llm;
using Andy.Model.Model;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>Unit tests for the rate-limit decorator (no network).</summary>
public class RateLimitTests
{
    [Fact]
    public void Policy_Detects_Transient_429_And_5xx_Only()
    {
        Assert.True(RateLimitPolicy.IsTransient("OpenRouter request failed (status 429): slow down"));
        Assert.True(RateLimitPolicy.IsTransient("request failed (status 503)"));
        Assert.True(RateLimitPolicy.IsTransient("Too Many Requests"));
        Assert.False(RateLimitPolicy.IsTransient("request failed (status 401): bad key"));
        Assert.False(RateLimitPolicy.IsTransient("request failed (status 404): model not found"));
    }

    [Fact]
    public async Task Decorator_Retries_429_Then_Succeeds()
    {
        var provider = new FlakyProvider(failures: 2, statusCode: 429);
        var policy = new RateLimitPolicy { MaxRetries = 5, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero };
        var sut = new RateLimitingLlmProvider(provider, policy, delay: (_, _) => Task.CompletedTask);

        var response = await sut.CompleteAsync(NewRequest());

        Assert.Equal(3, provider.Calls); // 2 failures + 1 success
        Assert.Equal("ok", response.Content);
    }

    [Fact]
    public async Task Decorator_Throws_Marked_Exception_When_Exhausted()
    {
        var provider = new FlakyProvider(failures: 100, statusCode: 429);
        var policy = new RateLimitPolicy { MaxRetries = 2, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero };
        var sut = new RateLimitingLlmProvider(provider, policy, delay: (_, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<RateLimitExhaustedException>(() => sut.CompleteAsync(NewRequest()));
        Assert.Contains(RateLimitExhaustedException.Marker, ex.Message);
        Assert.Equal(3, provider.Calls); // initial + 2 retries
    }

    [Fact]
    public async Task Decorator_Rethrows_NonTransient_Immediately()
    {
        var provider = new FlakyProvider(failures: 100, statusCode: 401);
        var policy = new RateLimitPolicy { MaxRetries = 5, BaseDelay = TimeSpan.Zero };
        var sut = new RateLimitingLlmProvider(provider, policy, delay: (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteAsync(NewRequest()));
        Assert.Equal(1, provider.Calls); // no retries on 401
    }

    private static LlmRequest NewRequest() => new()
    {
        Messages = new List<Message> { new() { Role = Role.User, Content = "hi" } },
    };

    private sealed class FlakyProvider : ILlmProvider
    {
        private readonly int _failures;
        private readonly int _statusCode;
        public int Calls { get; private set; }

        public FlakyProvider(int failures, int statusCode)
        {
            _failures = failures;
            _statusCode = statusCode;
        }

        public string Name => "flaky";

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls <= _failures)
                throw new InvalidOperationException($"request failed (status {_statusCode}): boom");
            return Task.FromResult(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "ok" },
            });
        }

        public IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IEnumerable<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<ModelInfo>());
    }
}
