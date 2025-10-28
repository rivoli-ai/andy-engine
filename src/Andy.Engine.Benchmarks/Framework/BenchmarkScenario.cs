namespace Andy.Benchmarks.Framework;

/// <summary>
/// Defines a benchmark scenario for testing Andy.Engine's capabilities
/// </summary>
public class BenchmarkScenario
{
    /// <summary>
    /// Unique identifier for the scenario
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Category this scenario belongs to (e.g., "bug-fixes", "feature-additions")
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Human-readable description of what this scenario tests
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Tags for filtering scenarios (e.g., "single-tool", "multi-turn")
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Workspace configuration (where the test code lives)
    /// </summary>
    public required WorkspaceConfig Workspace { get; init; }

    /// <summary>
    /// Context to inject into the engine (prompts, MCP data, etc.)
    /// </summary>
    public required ContextInjection Context { get; init; }

    /// <summary>
    /// Expected tool invocations (optional - for validation)
    /// </summary>
    public List<ExpectedToolInvocation> ExpectedTools { get; init; } = new();

    /// <summary>
    /// Validation criteria for this scenario
    /// </summary>
    public required ValidationConfig Validation { get; init; }

    /// <summary>
    /// Maximum time allowed for this scenario to complete
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
}