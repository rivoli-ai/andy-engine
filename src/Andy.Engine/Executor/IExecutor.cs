using Andy.Engine.Contracts;

namespace Andy.Engine.Executor;

/// <summary>
/// Interface for executing tool calls with validation and retry logic.
/// </summary>
public interface IExecutor
{
    /// <summary>
    /// Executes a tool call.
    /// </summary>
    /// <param name="call">The tool call to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tool execution result.</returns>
    Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken = default);
}