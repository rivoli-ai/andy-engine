using System.Runtime.CompilerServices;
using Andy.Model.Llm;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.SweBench.Llm;

/// <summary>
/// Decorates an <see cref="ILlmProvider"/> to transparently retry transient errors (HTTP 429
/// and 5xx surfaced by andy-llm as InvalidOperationException) with backoff. Non-transient
/// errors (401/403/400, etc.) are rethrown immediately so misconfiguration fails fast.
/// On exhaustion it throws <see cref="RateLimitExhaustedException"/>.
/// </summary>
public sealed class RateLimitingLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private readonly RateLimitPolicy _policy;
    private readonly ILogger? _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RateLimitingLlmProvider(
        ILlmProvider inner,
        RateLimitPolicy? policy = null,
        ILogger? logger = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _inner = inner;
        _policy = policy ?? new RateLimitPolicy();
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    public string Name => _inner.Name;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        _inner.IsAvailableAsync(cancellationToken);

    public Task<IEnumerable<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default) =>
        _inner.ListModelsAsync(cancellationToken);

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var lastError = string.Empty;
        for (var attempt = 0; attempt <= _policy.MaxRetries; attempt++)
        {
            try
            {
                return await _inner.CompleteAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && RateLimitPolicy.IsTransient(ex))
            {
                // Provider-agnostic: andy-llm's OpenRouter provider raises InvalidOperationException
                // with "(status 429)", while the OpenAI-SDK path raises ClientResultException with
                // "Status: 429". Both are recognized by IsTransient. A malformed/empty response body
                // (JsonException, e.g. a truncated stream) is also retried.
                lastError = ex.Message;
                if (attempt == _policy.MaxRetries)
                    break;

                var delay = _policy.NextDelay(attempt, ex.Message, jitterSeed: attempt + 1);
                _logger?.LogWarning(
                    "Transient LLM error (attempt {Attempt}/{Max}); backing off {Delay}. {Error}",
                    attempt + 1, _policy.MaxRetries, delay, Truncate(ex.Message));
                await _delay(delay, cancellationToken);
            }
        }

        throw new RateLimitExhaustedException(_policy.MaxRetries + 1, Truncate(lastError));
    }

    /// <summary>
    /// Streams with the same transient-retry protection as <see cref="CompleteAsync"/>, but only
    /// until the first chunk reaches the consumer: once output has been yielded, a restart would
    /// duplicate it, so later failures propagate. Providers that surface HTTP failures as an error
    /// CHUNK (rather than throwing) are also covered — a transient error chunk arriving before any
    /// real output triggers a retry instead of being handed to the consumer.
    /// </summary>
    public async IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastError = string.Empty;
        for (var attempt = 0; attempt <= _policy.MaxRetries; attempt++)
        {
            string? retryError = null;
            var yielded = false;
            var enumerator = _inner.StreamCompleteAsync(request, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    bool moved;
                    LlmStreamResponse? current = null;
                    try
                    {
                        moved = await enumerator.MoveNextAsync();
                        if (moved)
                            current = enumerator.Current;
                    }
                    catch (Exception ex) when (!yielded &&
                                               ex is not OperationCanceledException &&
                                               RateLimitPolicy.IsTransient(ex))
                    {
                        retryError = ex.Message;
                        break;
                    }

                    if (!moved)
                        yield break;

                    if (!yielded && current!.Error is { Length: > 0 } err && RateLimitPolicy.IsTransient(err))
                    {
                        retryError = err;
                        break;
                    }

                    yielded = true;
                    yield return current!;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            lastError = retryError!;
            if (attempt == _policy.MaxRetries)
                break;

            var delay = _policy.NextDelay(attempt, lastError, jitterSeed: attempt + 1);
            _logger?.LogWarning(
                "Transient LLM stream error (attempt {Attempt}/{Max}); backing off {Delay}. {Error}",
                attempt + 1, _policy.MaxRetries, delay, Truncate(lastError));
            await _delay(delay, cancellationToken);
        }

        throw new RateLimitExhaustedException(_policy.MaxRetries + 1, Truncate(lastError));
    }

    private static string Truncate(string s, int max = 300) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
