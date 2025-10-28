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

/// <summary>
/// Record of a tool invocation
/// </summary>
public class ToolInvocationRecord
{
    /// <summary>
    /// Type of tool invoked
    /// </summary>
    public required string ToolType { get; init; }

    /// <summary>
    /// Parameters passed to the tool
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Result returned by the tool
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Whether the tool invocation succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp of the invocation
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Duration of the tool execution
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Record of an LLM interaction (request/response pair)
/// </summary>
public class LlmInteraction
{
    /// <summary>
    /// User or system prompt sent to LLM
    /// </summary>
    public required string Request { get; init; }

    /// <summary>
    /// LLM response
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Timestamp of the interaction
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Token count for the request
    /// </summary>
    public int RequestTokens { get; init; }

    /// <summary>
    /// Token count for the response
    /// </summary>
    public int ResponseTokens { get; init; }

    /// <summary>
    /// Model used for this interaction
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
/// Result of a validation step
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Name of the validator
    /// </summary>
    public required string ValidatorName { get; init; }

    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Detailed message
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Additional details (errors, warnings, etc.)
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Performance metrics for a benchmark run
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Total number of tool invocations
    /// </summary>
    public int TotalToolInvocations { get; init; }

    /// <summary>
    /// Total number of LLM interactions
    /// </summary>
    public int TotalLlmInteractions { get; init; }

    /// <summary>
    /// Total tokens used (input + output)
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Number of files changed
    /// </summary>
    public int FilesChanged { get; init; }

    /// <summary>
    /// Number of lines added
    /// </summary>
    public int LinesAdded { get; init; }

    /// <summary>
    /// Number of lines removed
    /// </summary>
    public int LinesRemoved { get; init; }

    /// <summary>
    /// Time spent in LLM calls
    /// </summary>
    public TimeSpan LlmTime { get; init; }

    /// <summary>
    /// Time spent in tool execution
    /// </summary>
    public TimeSpan ToolTime { get; init; }
}
