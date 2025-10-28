namespace Andy.Engine.Benchmarks.Framework;

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

    /// <summary>
    /// Size of the request context in characters
    /// </summary>
    public int ContextSize { get; init; }
}