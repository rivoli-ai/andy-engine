using System.Diagnostics;
using Andy.Engine;
using Andy.Engine.Benchmarks.Framework;
using Andy.Tools.Core;

namespace Andy.Benchmarks.Framework;

/// <summary>
/// Runs benchmark scenarios using SimpleAgent and captures results
/// </summary>
public class SimpleScenarioRunner
{
    private readonly SimpleAgent _agent;
    private readonly string _workspaceRoot;

    public SimpleScenarioRunner(SimpleAgent agent, string workspaceRoot)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
    }

    /// <summary>
    /// Executes a benchmark scenario and returns the result
    /// </summary>
    public async Task<BenchmarkResult> RunAsync(
        BenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        var toolInvocations = new List<ToolInvocationRecord>();

        try
        {
            // Set up event handlers to capture tool invocations
            void OnToolCalled(object? sender, ToolCalledEventArgs e)
            {
                toolInvocations.Add(new ToolInvocationRecord
                {
                    ToolType = e.ToolName,
                    Parameters = new Dictionary<string, object>(), // Will be filled by capturing executor
                    Result = e.Result,
                    Success = true,
                    Timestamp = DateTime.UtcNow,
                    Duration = TimeSpan.Zero
                });
            }

            _agent.ToolCalled += OnToolCalled;

            try
            {
                // Get the user prompt from scenario
                var userMessage = scenario.Context.Prompts.First();

                // Run the simple agent
                var agentResult = await _agent.ProcessMessageAsync(userMessage, cancellationToken);

                sw.Stop();

                return new BenchmarkResult
                {
                    ScenarioId = scenario.Id,
                    Success = agentResult.Success,
                    Duration = sw.Elapsed,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    ToolInvocations = toolInvocations,
                    ErrorMessage = agentResult.Success ? null : agentResult.StopReason
                };
            }
            finally
            {
                _agent.ToolCalled -= OnToolCalled;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();

            return new BenchmarkResult
            {
                ScenarioId = scenario.Id,
                Success = false,
                Duration = sw.Elapsed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ToolInvocations = toolInvocations,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace
            };
        }
    }
}
