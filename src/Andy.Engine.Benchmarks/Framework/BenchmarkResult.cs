namespace Andy.Engine.Benchmarks.Framework;

/// <summary>
/// Result of executing a benchmark scenario
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// ID of the scenario that was run
    /// </summary>
    public required string ScenarioId { get; init; }

    /// <summary>
    /// Overall success status
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Time taken to execute the scenario
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Timestamp when the scenario started
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the scenario completed
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Tool invocations captured during execution
    /// </summary>
    public List<ToolInvocationRecord> ToolInvocations { get; init; } = new();

    /// <summary>
    /// LLM interactions captured during execution
    /// </summary>
    public List<LlmInteraction> LlmInteractions { get; init; } = new();

    /// <summary>
    /// Validation results
    /// </summary>
    public List<ValidationResult> ValidationResults { get; init; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public PerformanceMetrics Metrics { get; init; } = new();

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Stack trace (if failed with exception)
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}