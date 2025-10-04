using System.Diagnostics;
using Andy.Engine;
using Andy.Engine.Contracts;
using Andy.Engine.Policy;

namespace Andy.Benchmarks.Framework;

/// <summary>
/// Runs benchmark scenarios and captures results
/// </summary>
public class ScenarioRunner
{
    private readonly Agent _agent;
    private readonly string _workspaceRoot;

    public ScenarioRunner(Agent agent, string workspaceRoot)
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
        var llmInteractions = new List<LlmInteraction>();

        try
        {
            // Set up workspace
            var workspace = await SetupWorkspaceAsync(scenario.Workspace, cancellationToken);

            // Set up event handlers to capture tool invocations
            void OnToolCalled(object? sender, ToolCalledEventArgs e)
            {
                toolInvocations.Add(new ToolInvocationRecord
                {
                    ToolType = e.ToolName,
                    Result = e.Result,
                    Success = true,
                    Timestamp = DateTime.UtcNow,
                    Duration = TimeSpan.Zero // Will be updated if we have timing info
                });
            }

            _agent.ToolCalled += OnToolCalled;

            try
            {
                // Create goal from first prompt
                var goal = new AgentGoal(
                    UserGoal: scenario.Context.Prompts.First(),
                    Constraints: Array.Empty<string>()
                );

                // Set up budget
                var budget = new Budget(
                    MaxTurns: 50,
                    MaxWallClock: scenario.Timeout
                );

                // Set up error policy
                var errorPolicy = new ErrorHandlingPolicy(
                    MaxRetries: 3,
                    BaseBackoff: TimeSpan.FromSeconds(1),
                    UseFallbacks: true,
                    AskUserWhenMissingFields: false
                );

                // Run the agent
                var agentResult = await _agent.RunAsync(
                    goal,
                    budget,
                    errorPolicy,
                    cancellationToken
                );

                sw.Stop();

                // Create benchmark result
                return new BenchmarkResult
                {
                    ScenarioId = scenario.Id,
                    Success = agentResult.Success,
                    Duration = sw.Elapsed,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    ToolInvocations = toolInvocations,
                    LlmInteractions = llmInteractions,
                    ValidationResults = new List<ValidationResult>(),
                    Metrics = new PerformanceMetrics
                    {
                        TotalToolInvocations = toolInvocations.Count,
                        TotalLlmInteractions = llmInteractions.Count,
                        TotalTokens = llmInteractions.Sum(i => i.RequestTokens + i.ResponseTokens)
                    }
                };
            }
            finally
            {
                _agent.ToolCalled -= OnToolCalled;
                await CleanupWorkspaceAsync(workspace, cancellationToken);
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
                LlmInteractions = llmInteractions,
                ValidationResults = new List<ValidationResult>(),
                Metrics = new PerformanceMetrics(),
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace
            };
        }
    }

    private async Task<string> SetupWorkspaceAsync(
        WorkspaceConfig config,
        CancellationToken cancellationToken)
    {
        var workspacePath = Path.Combine(_workspaceRoot, Guid.NewGuid().ToString());
        Directory.CreateDirectory(workspacePath);

        switch (config.Type.ToLowerInvariant())
        {
            case "git-clone":
                await CloneGitRepositoryAsync(config.Source, workspacePath, config.Branch, cancellationToken);
                break;

            case "directory-copy":
                await CopyDirectoryAsync(config.Source, workspacePath, cancellationToken);
                break;

            case "in-memory":
                // Create empty workspace - files will be injected via context
                break;

            default:
                throw new NotSupportedException($"Workspace type '{config.Type}' is not supported");
        }

        return workspacePath;
    }

    private async Task CloneGitRepositoryAsync(
        string source,
        string destination,
        string? branch,
        CancellationToken cancellationToken)
    {
        // For now, use simple directory copy if source is local path
        if (Directory.Exists(source))
        {
            await CopyDirectoryAsync(source, destination, cancellationToken);
            return;
        }

        // TODO: Implement actual git clone for remote repositories
        throw new NotImplementedException("Git clone from remote repositories not yet implemented");
    }

    private Task CopyDirectoryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(source, dir);
                var targetPath = Path.Combine(destination, relativePath);
                Directory.CreateDirectory(targetPath);
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                // Skip git and build artifacts
                if (file.Contains(".git") || file.Contains("bin") || file.Contains("obj"))
                    continue;

                var relativePath = Path.GetRelativePath(source, file);
                var targetPath = Path.Combine(destination, relativePath);
                File.Copy(file, targetPath, overwrite: true);
            }
        }, cancellationToken);
    }

    private Task CleanupWorkspaceAsync(string workspace, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }, cancellationToken);
    }
}
