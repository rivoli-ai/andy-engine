namespace Andy.Engine.SweBench.Model;

/// <summary>
/// A single SWE-bench (Verified) task instance, parsed from the dataset.
///
/// Fields mirror the official dataset schema (princeton-nlp/SWE-bench_Verified).
/// Note that <see cref="GoldPatch"/> and <see cref="TestPatch"/> are the answer key:
/// they are used ONLY by the grader and must NEVER be exposed to the agent.
/// </summary>
public sealed class SweBenchInstance
{
    /// <summary>Formatted identifier, e.g. "sympy__sympy-20590".</summary>
    public required string InstanceId { get; init; }

    /// <summary>GitHub "owner/name", e.g. "sympy/sympy".</summary>
    public required string Repo { get; init; }

    /// <summary>Commit hash representing repo HEAD before the solution PR.</summary>
    public required string BaseCommit { get; init; }

    /// <summary>Issue title + body. This is the agent's input.</summary>
    public required string ProblemStatement { get; init; }

    /// <summary>Installation/test-spec version selector (repo+version picks the test command).</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Commit used for environment setup/installation (may differ from base_commit).</summary>
    public string? EnvironmentSetupCommit { get; init; }

    /// <summary>Tests that should transition from failing to passing once the issue is resolved.</summary>
    public IReadOnlyList<string> FailToPass { get; init; } = Array.Empty<string>();

    /// <summary>Tests that should pass both before and after the patch.</summary>
    public IReadOnlyList<string> PassToPass { get; init; } = Array.Empty<string>();

    // ---- Answer key: grader-only, never shown to the agent ----

    /// <summary>Gold patch from the PR (minus tests). Used for the gold-validation gate only.</summary>
    public string GoldPatch { get; init; } = string.Empty;

    /// <summary>Official test patch, applied by the grader (not the agent).</summary>
    public string TestPatch { get; init; } = string.Empty;
}
