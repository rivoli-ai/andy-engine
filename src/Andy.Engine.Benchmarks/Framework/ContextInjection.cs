namespace Andy.Benchmarks.Framework;

/// <summary>
/// Context data to inject into the engine
/// </summary>
public class ContextInjection
{
    /// <summary>
    /// User prompts (single or multi-turn conversation)
    /// </summary>
    public List<string> Prompts { get; init; } = new();

    /// <summary>
    /// Simulated MCP server responses
    /// </summary>
    public Dictionary<string, object> McpServerData { get; init; } = new();

    /// <summary>
    /// Simulated API tool responses
    /// </summary>
    public Dictionary<string, object> ApiToolData { get; init; } = new();

    /// <summary>
    /// Additional files to inject into context (path -> content)
    /// </summary>
    public Dictionary<string, string> InjectedFiles { get; init; } = new();
}