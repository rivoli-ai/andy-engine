namespace Andy.Engine.Benchmarks.Framework;

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