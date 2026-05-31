namespace Andy.Engine;

/// <summary>
/// Event raised when a tool is called.
/// </summary>
public class ToolCalledEventArgs : EventArgs
{
    public Guid TraceId { get; }
    public string ToolName { get; }
    public string Result { get; }

    public ToolCalledEventArgs(Guid traceId, string toolName, string result)
    {
        TraceId = traceId;
        ToolName = toolName;
        Result = result;
    }
}
