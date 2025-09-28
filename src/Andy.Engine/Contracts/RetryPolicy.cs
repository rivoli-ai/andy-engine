namespace Andy.Engine.Contracts;

/// <summary>
/// Defines retry behavior for tool execution.
/// </summary>
public sealed record RetryPolicy(
    int MaxRetries = 3,
    TimeSpan BaseBackoff = default,
    BackoffStrategy Strategy = BackoffStrategy.ExponentialWithJitter,
    double JitterFactor = 0.1
)
{
    public static RetryPolicy Default => new(
        MaxRetries: 3,
        BaseBackoff: TimeSpan.FromSeconds(1),
        Strategy: BackoffStrategy.ExponentialWithJitter,
        JitterFactor: 0.1
    );

    public static RetryPolicy NoRetry => new(MaxRetries: 0);
}

public enum BackoffStrategy
{
    None,
    Linear,
    Exponential,
    ExponentialWithJitter
}