namespace Andy.Engine;

/// <summary>
/// Opt-in limits and checkpoint behavior for continuing a request beyond one
/// <see cref="SimpleAgent"/> turn-budget window.
/// </summary>
public sealed record AgentContinuationPolicy
{
    /// <summary>
    /// Hard ceiling for all LLM turns across the initial and continued windows.
    /// </summary>
    public required int MaxTotalTurns { get; init; }

    /// <summary>
    /// Maximum number of windows after the initial window. Each continued window creates one
    /// compact checkpoint, so this also bounds checkpoint/compaction count.
    /// </summary>
    public int MaxContinuationWindows { get; init; } = 2;

    /// <summary>
    /// Optional wall-clock ceiling for the whole request, including tools and checkpoint creation.
    /// </summary>
    public TimeSpan? MaxElapsedTime { get; init; }

    /// <summary>
    /// Number of recent, complete assistant tool-call/result rounds retained verbatim beside the
    /// compact checkpoint in the next request window.
    /// </summary>
    public int RecentToolCallRounds { get; init; } = 3;

    /// <summary>
    /// Maximum checkpoint text length sent to the provider.
    /// </summary>
    public int MaxCheckpointChars { get; init; } = 12_000;

    /// <summary>
    /// Number of equivalent checkpoint reappearances tolerated before the run stops with
    /// <c>continuation_no_progress</c>. A value of 1 stops at the first repeat, including an
    /// oscillation that returns to an earlier checkpoint after an intervening window.
    /// </summary>
    public int EquivalentCheckpointLimit { get; init; } = 1;

    /// <summary>
    /// Optional asynchronous checkpoint formatter. The default formatter creates a bounded,
    /// structured checkpoint from the objective and observed tool outcomes. Custom formatters
    /// must not execute or replay tools; they receive immutable checkpoint data and cancellation.
    /// </summary>
    public Func<AgentCheckpointContext, CancellationToken, ValueTask<string>>? CheckpointFactory { get; init; }
}

/// <summary>
/// Immutable input supplied to a continuation checkpoint factory.
/// </summary>
public sealed record AgentCheckpointContext
{
    /// <summary>The original user objective for the run.</summary>
    public required string Objective { get; init; }

    /// <summary>The agent working directory in which prior tool side effects may exist.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>The 1-based window number that just reached its per-window limit.</summary>
    public required int CompletedWindow { get; init; }

    /// <summary>Total LLM turns dispatched before checkpoint creation.</summary>
    public required int TotalTurns { get; init; }

    /// <summary>All correlated tool outcomes executed so far in this user request.</summary>
    public required IReadOnlyList<AgentCheckpointToolOutcome> ToolOutcomes { get; init; }
}

/// <summary>
/// One already-executed tool outcome represented in a continuation checkpoint.
/// </summary>
public sealed record AgentCheckpointToolOutcome
{
    /// <summary>The registered tool identifier.</summary>
    public required string ToolName { get; init; }

    /// <summary>The exact argument JSON supplied by the model.</summary>
    public required string ArgumentsJson { get; init; }

    /// <summary>The bounded result JSON recorded in the agent transcript.</summary>
    public required string ResultJson { get; init; }

    /// <summary>Whether the recorded tool result represents an error.</summary>
    public required bool IsError { get; init; }
}

/// <summary>
/// Lifecycle stages emitted while bounded continuation is enabled.
/// </summary>
public enum AgentContinuationEventKind
{
    /// <summary>A new per-window turn budget became active.</summary>
    WindowStarted,

    /// <summary>A window consumed its complete per-window turn budget.</summary>
    WindowCompleted,

    /// <summary>A compact checkpoint was created for a continued window.</summary>
    CheckpointCreated,

    /// <summary>A repeated or oscillating progress checkpoint tripped the guard.</summary>
    NoProgressDetected,

    /// <summary>A global continuation ceiling stopped the run.</summary>
    Stopped,

    /// <summary>The model returned a final response while continuation policy was enabled.</summary>
    Completed,
}

/// <summary>
/// Structured continuation lifecycle event for terminal, TUI, ACP, and headless hosts.
/// </summary>
public sealed class AgentContinuationEventArgs : EventArgs
{
    /// <summary>Correlation identifier shared by every continuation event for one request.</summary>
    public required string RunId { get; init; }

    /// <summary>The lifecycle stage represented by this event.</summary>
    public required AgentContinuationEventKind Kind { get; init; }

    /// <summary>The active or just-completed 1-based window number.</summary>
    public required int WindowNumber { get; init; }

    /// <summary>Total LLM turns dispatched at the time of the event.</summary>
    public required int TotalTurns { get; init; }

    /// <summary>UTC event creation time.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Checkpoint text for checkpoint-related events; otherwise null.</summary>
    public string? Checkpoint { get; init; }

    /// <summary>Machine-readable stop reason for terminal bounded stops; otherwise null.</summary>
    public string? StopReason { get; init; }
}
