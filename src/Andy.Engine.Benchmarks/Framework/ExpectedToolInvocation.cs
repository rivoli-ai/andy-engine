namespace Andy.Benchmarks.Framework;

/// <summary>
/// Expected tool invocation for validation
/// </summary>
public class ExpectedToolInvocation
{
    /// <summary>
    /// Type of tool (e.g., "ReadFile", "WriteFile", "EditFile")
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Expected parameters (key-value pairs)
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Pattern to match against file paths (supports wildcards)
    /// </summary>
    public string? PathPattern { get; init; }

    /// <summary>
    /// Minimum number of times this tool should be invoked
    /// </summary>
    public int MinInvocations { get; init; } = 1;

    /// <summary>
    /// Maximum number of times this tool should be invoked
    /// </summary>
    public int? MaxInvocations { get; init; }
}