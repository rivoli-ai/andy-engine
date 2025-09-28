using System.Text.Json.Nodes;

namespace Andy.Engine.Contracts;

/// <summary>
/// Result of a tool execution including success status, data, and error information.
/// </summary>
public sealed record ToolResult(
    bool Ok,
    JsonNode? Data,
    ToolErrorCode ErrorCode = ToolErrorCode.None,
    string? ErrorDetails = null,
    bool SchemaValidated = false,
    int Attempt = 1,
    TimeSpan Latency = default
);

public enum ToolErrorCode
{
    None,
    InvalidInput,
    Timeout,
    RetryableServer,
    RateLimited,
    OutputSchemaMismatch,
    NoResults,
    ToolBug,
    Unauthorized,
    Forbidden,
    NotFound
}