namespace Andy.Engine.SweBench.Llm;

/// <summary>
/// Thrown when transient-error retries are exhausted. Because <c>SimpleAgent</c> swallows
/// exceptions into its StopReason string, the message embeds <see cref="Marker"/> so the
/// fail-fast gate can recognize an exhausted-rate-limit failure from that string.
/// </summary>
public sealed class RateLimitExhaustedException : Exception
{
    public const string Marker = "RATE_LIMIT_EXHAUSTED";

    public RateLimitExhaustedException(int attempts, string lastError)
        : base($"{Marker}: gave up after {attempts} attempts. Last error: {lastError}")
    {
    }
}
