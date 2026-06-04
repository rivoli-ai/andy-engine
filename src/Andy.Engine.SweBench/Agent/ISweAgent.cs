namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// The outcome of running an agent over one instance's workspace. The harness captures the
/// model patch separately (via <c>git diff</c>), so this only conveys run-control signals:
/// whether the agent finished cleanly and why it stopped, for fail-fast accounting.
/// </summary>
public sealed record SweAgentRunResult(
    bool Success,
    string StopReason,
    int TurnCount,
    TimeSpan Duration);

/// <summary>
/// A pluggable coding agent bound to a single prepared workspace (a git working tree at the
/// instance's base commit). The contract is deliberately narrow: given the problem statement,
/// mutate files in the workspace to fix the issue. Patch capture and grading are the harness's
/// job (it diffs the tree afterward), so an implementation never produces a patch itself — it
/// just edits files. This lets the andy in-process agent and any external CLI agent (opencode,
/// aider, ...) be compared on the exact same dataset, grader, and report.
/// </summary>
public interface ISweAgent : IDisposable
{
    Task<SweAgentRunResult> RunAsync(string problemStatement, CancellationToken cancellationToken = default);
}

/// <summary>Creates an <see cref="ISweAgent"/> scoped to a given workspace directory.</summary>
public interface ISweAgentFactory
{
    ISweAgent Create(string workspaceDir);
}
