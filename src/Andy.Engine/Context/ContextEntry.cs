namespace Andy.Engine.Context;

/// <summary>
/// Represents a single entry in the context history
/// </summary>
public class ContextEntry
{
    public MessageRole Role { get; set; }
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? ToolId { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolResult { get; set; }
    public List<ContextToolCall>? ToolCalls { get; set; }
}