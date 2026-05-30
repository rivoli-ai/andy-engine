namespace Andy.Engine.SweBench.Model;

/// <summary>
/// Outcome of grading a single instance's prediction inside its Docker environment.
/// </summary>
public sealed class InstanceGradeResult
{
    public required string InstanceId { get; init; }

    /// <summary>Whether the model_patch applied cleanly. False =&gt; unresolved.</summary>
    public bool PatchApplied { get; init; }

    /// <summary>
    /// Whether the official test_patch applied. False here indicates a harness/env
    /// problem (the official tests should always apply to base_commit).
    /// </summary>
    public bool TestPatchApplied { get; init; }

    /// <summary>Parsed per-test statuses (test id -&gt; status). Missing ids count as failures.</summary>
    public IReadOnlyDictionary<string, TestStatus> StatusMap { get; init; } =
        new Dictionary<string, TestStatus>();

    public ResolvedStatus ResolvedStatus { get; init; } = ResolvedStatus.No;

    /// <summary>True only when <see cref="ResolvedStatus"/> is Full.</summary>
    public bool Resolved => ResolvedStatus == ResolvedStatus.Full;

    /// <summary>An empty prediction patch was submitted.</summary>
    public bool EmptyPatch { get; init; }

    /// <summary>Non-null when grading could not complete (harness/env error, timeout, etc.).</summary>
    public string? Error { get; init; }

    /// <summary>Whether grading hit the per-instance timeout.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Raw combined test output, for the per-instance log.</summary>
    public string RawTestLog { get; init; } = string.Empty;
}
