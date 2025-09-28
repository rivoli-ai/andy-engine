using System.Text.Json;
using System.Text.Json.Nodes;

namespace Andy.Engine.Contracts;

/// <summary>
/// Specification for a tool including its schema, retry policy, and limits.
/// </summary>
public sealed record ToolSpec(
    string Name,
    Version Version,
    JsonNode InputSchema,
    JsonNode OutputSchema,
    RetryPolicy RetryPolicy,
    TimeSpan Timeout,
    int PageLimit = 100,
    int MaxPayloadBytes = 512_000
);