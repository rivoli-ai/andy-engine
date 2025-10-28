using Andy.Tools.Core;
using Andy.Tools.Execution;

namespace Andy.Engine.Benchmarks.Framework;

/// <summary>
/// Wraps a tool executor to capture tool invocations for benchmarking
/// </summary>
public class CapturingToolExecutor : IToolExecutor
{
    private readonly IToolExecutor _inner;
    private readonly List<(string ToolName, Dictionary<string, object> Parameters)> _capturedInvocations = new();
    private readonly List<ToolExecutionResult> _capturedResults = new();

    public CapturingToolExecutor(IToolExecutor inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        // Forward events from inner executor and capture results
        _inner.ExecutionStarted += (s, e) => ExecutionStarted?.Invoke(this, e);
        _inner.ExecutionCompleted += (s, e) =>
        {
            // Capture the result
            if (e.Result != null)
            {
                _capturedResults.Add(e.Result);
            }
            ExecutionCompleted?.Invoke(this, e);
        };
        _inner.SecurityViolation += (s, e) => SecurityViolation?.Invoke(this, e);
    }

    public List<(string ToolName, Dictionary<string, object> Parameters)> CapturedInvocations => _capturedInvocations;
    public List<ToolExecutionResult> CapturedResults => _capturedResults;

    // Events - required by IToolExecutor
    public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted;
    public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted;
    public event EventHandler<SecurityViolationEventArgs>? SecurityViolation;

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request)
    {
        // Capture parameters
        var paramsCopy = new Dictionary<string, object>();
        if (request.Parameters != null)
        {
            foreach (var kvp in request.Parameters)
            {
                if (kvp.Value != null)
                {
                    paramsCopy[kvp.Key] = kvp.Value;
                }
            }
        }
        _capturedInvocations.Add((request.ToolId, paramsCopy));

        // Execute
        return _inner.ExecuteAsync(request);
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        string toolId,
        Dictionary<string, object?> parameters,
        ToolExecutionContext? context = null)
    {
        // Capture parameters
        var paramsCopy = new Dictionary<string, object>();
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                if (kvp.Value != null)
                {
                    paramsCopy[kvp.Key] = kvp.Value;
                }
            }
        }
        _capturedInvocations.Add((toolId, paramsCopy));

        // Execute
        return _inner.ExecuteAsync(toolId, parameters, context);
    }

    public Task<IList<string>> ValidateExecutionRequestAsync(ToolExecutionRequest request)
    {
        return _inner.ValidateExecutionRequestAsync(request);
    }

    public Task<ToolResourceUsage?> EstimateResourceUsageAsync(
        string toolId,
        Dictionary<string, object?> parameters)
    {
        return _inner.EstimateResourceUsageAsync(toolId, parameters);
    }

    public Task<int> CancelExecutionsAsync(string correlationId)
    {
        return _inner.CancelExecutionsAsync(correlationId);
    }

    public IReadOnlyList<RunningExecutionInfo> GetRunningExecutions()
    {
        return _inner.GetRunningExecutions();
    }

    public ToolExecutionStatistics GetStatistics()
    {
        return _inner.GetStatistics();
    }

    public void ClearCaptured()
    {
        _capturedInvocations.Clear();
        _capturedResults.Clear();
    }
}
