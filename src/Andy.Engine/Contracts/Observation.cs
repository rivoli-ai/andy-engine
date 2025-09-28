namespace Andy.Engine.Contracts;

/// <summary>
/// Normalized observation from a tool execution.
/// </summary>
public sealed record Observation(
    string Summary,
    IReadOnlyDictionary<string, string> KeyFacts,
    IReadOnlyList<string> Affordances,
    ToolResult Raw
);