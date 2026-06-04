using Andy.Engine.SweBench.Llm;
using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Orchestration;

namespace Andy.Engine.SweBench.Agent;

/// <summary>Outcome of running the agent on one instance.</summary>
public sealed record AgentRunResult
{
    public required SwePrediction Prediction { get; init; }
    public required InstanceOutcome Outcome { get; init; }
    public required string Detail { get; init; }
    public int TurnCount { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Runs the agent for a single instance: provision workspace, run the agent on the problem
/// statement, capture the model patch, and classify the outcome for fail-fast accounting.
/// </summary>
public sealed class SweInstanceRunner
{
    private readonly RunContext _ctx;
    private readonly SweWorkspaceManager _workspaces;
    private readonly ISweAgentFactory _agentFactory;
    private readonly TextWriter? _log;

    public SweInstanceRunner(RunContext ctx, SweWorkspaceManager workspaces, TextWriter? log = null)
        : this(ctx, workspaces, SelectAgentFactory(ctx), log)
    {
    }

    /// <summary>Test/DI seam: inject an arbitrary agent factory (e.g. a fake or a CLI agent).</summary>
    public SweInstanceRunner(RunContext ctx, SweWorkspaceManager workspaces, ISweAgentFactory agentFactory, TextWriter? log = null)
    {
        _ctx = ctx;
        _workspaces = workspaces;
        _agentFactory = agentFactory;
        _log = log;
    }

    /// <summary>Picks the agent implementation from <see cref="RunContext.Agent"/>.</summary>
    public static ISweAgentFactory SelectAgentFactory(RunContext ctx) => ctx.Agent.ToLowerInvariant() switch
    {
        "andy" => new SweAgentFactory(ctx),
        "external" => new ExternalCliAgentFactory(ctx),
        var other => throw new ArgumentException($"Unknown --agent '{other}'. Expected andy|external."),
    };

    public async Task<AgentRunResult> RunAsync(SweBenchInstance instance, CancellationToken cancellationToken = default)
    {
        string workspace;
        try
        {
            workspace = await _workspaces.PrepareAsync(instance, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Clone/checkout failure is a setup problem. (A cancellation/timeout propagates so the
            // orchestrator can record a clean timeout instead of a setup error.)
            return Hard(instance, string.Empty, $"workspace prepare failed: {ex.Message}");
        }

        try
        {
            using var swe = _agentFactory.Create(workspace);

            var result = await swe.RunAsync(instance.ProblemStatement, cancellationToken);
            var patch = await _workspaces.GetModelPatchAsync(workspace, instance, cancellationToken);

            var prediction = new SwePrediction
            {
                InstanceId = instance.InstanceId,
                ModelNameOrPath = _ctx.Model,
                ModelPatch = patch,
            };

            var outcome = ClassifyOutcome(result.Success, result.StopReason);
            var detail = result.Success
                ? (prediction.IsEmptyPatch ? "produced empty patch" : $"produced patch ({patch.Length} bytes)")
                : $"agent stopped: {result.StopReason}";

            return new AgentRunResult
            {
                Prediction = prediction,
                Outcome = outcome,
                Detail = detail,
                TurnCount = result.TurnCount,
                Duration = result.Duration,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Hard(instance, string.Empty, $"agent run threw: {ex.Message}");
        }
        // OperationCanceledException propagates (a per-instance timeout or a real cancellation);
        // the orchestrator decides whether to skip the instance or stop the run.
        finally
        {
            if (!_ctx.KeepWorkspaces)
                _workspaces.Cleanup(workspace);
        }
    }

    /// <summary>
    /// A failed agent run is a HARD error only when it signals broken setup: exhausted rate
    /// limit, or auth/model errors (401/403/404). Everything else (max turns, empty output,
    /// model-patch-didn't-apply later) is a SOFT, instance-specific outcome.
    /// </summary>
    private static InstanceOutcome ClassifyOutcome(bool success, string stopReason)
    {
        if (success)
            return InstanceOutcome.Soft;

        if (stopReason.Contains(RateLimitExhaustedException.Marker, StringComparison.Ordinal))
            return InstanceOutcome.HardError;

        foreach (var status in new[] { "(status 401)", "(status 403)", "(status 404)" })
            if (stopReason.Contains(status, StringComparison.OrdinalIgnoreCase))
                return InstanceOutcome.HardError;

        return InstanceOutcome.Soft;
    }

    private static AgentRunResult Hard(SweBenchInstance instance, string patch, string detail) =>
        new()
        {
            Prediction = new SwePrediction
            {
                InstanceId = instance.InstanceId,
                ModelNameOrPath = "error",
                ModelPatch = patch,
            },
            Outcome = InstanceOutcome.HardError,
            Detail = detail,
        };
}
