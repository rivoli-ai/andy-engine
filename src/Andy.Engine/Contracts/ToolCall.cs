using System.Text.Json.Nodes;

namespace Andy.Engine.Contracts;

/// <summary>
/// Represents a tool call with its name and arguments.
/// </summary>
public sealed record ToolCall(string ToolName, JsonNode Args);