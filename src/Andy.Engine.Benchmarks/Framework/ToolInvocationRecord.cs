namespace Andy.Engine.Benchmarks.Framework;

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
    public Dictionary<string, object> Parameters { get; set; } = new();

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