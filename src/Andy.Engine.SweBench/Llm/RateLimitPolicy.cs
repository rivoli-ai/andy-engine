using System.Text.RegularExpressions;

namespace Andy.Engine.SweBench.Llm;

/// <summary>
/// Backoff policy for transient LLM errors. Honors a Retry-After hint when present in the
/// error message, otherwise uses capped exponential backoff with jitter.
/// </summary>
public sealed partial class RateLimitPolicy
{
    public int MaxRetries { get; init; } = 6;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(60);

    [GeneratedRegex(@"retry[\s\-]?after[""'\s:=]+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex RetryAfterRegex();

    // Matches "(status 429)", "Status: 429", "status code 429", "HTTP 429".
    [GeneratedRegex(@"(?:\(status\s+|status[:\s]+(?:code\s+)?|http\s+)(\d{3})", RegexOptions.IgnoreCase)]
    private static partial Regex StatusRegex();

    /// <summary>True if the error message indicates a retryable condition (429 or 5xx).</summary>
    public static bool IsTransient(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        var m = StatusRegex().Match(message);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var status))
            return status == 429 || (status >= 500 && status <= 599);

        // Fall back to textual hints when no explicit status is present.
        return message.Contains("429", StringComparison.Ordinal)
            || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
            || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes the delay for a 0-based attempt index. A jitter seed (e.g. attempt + id hash)
    /// keeps the backoff deterministic-but-spread without using a process-wide RNG.
    /// </summary>
    public TimeSpan NextDelay(int attempt, string errorMessage, int jitterSeed = 0)
    {
        if (TryParseRetryAfter(errorMessage, out var retryAfter))
            return Clamp(retryAfter);

        var exp = BaseDelay.TotalSeconds * Math.Pow(2, attempt);
        // Deterministic jitter in [0, 1) from the seed, so retries spread out without RNG.
        var jitter = ((jitterSeed * 2654435761u) % 1000) / 1000.0;
        return Clamp(TimeSpan.FromSeconds(exp * (1.0 + 0.25 * jitter)));
    }

    private static bool TryParseRetryAfter(string message, out TimeSpan delay)
    {
        var m = RetryAfterRegex().Match(message);
        if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var secs))
        {
            delay = TimeSpan.FromSeconds(secs);
            return true;
        }
        delay = default;
        return false;
    }

    private TimeSpan Clamp(TimeSpan d) => d > MaxDelay ? MaxDelay : (d < TimeSpan.Zero ? TimeSpan.Zero : d);
}
