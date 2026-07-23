using Andy.Model.Llm;
using Andy.Tools.Core;
using System.Runtime.CompilerServices;

namespace Andy.Engine;

/// <summary>
/// Contract for one delegated unit of work executed by a bounded child agent (issue #34).
/// Every field is validated against the parent's ceilings before ANY child in the batch starts;
/// a child can never widen its workspace, tools, provider policy, or budgets beyond its parent.
/// </summary>
public sealed record ChildTask
{
    /// <summary>
    /// Stable identifier used in events and results; defaults to "child-{taskIndex}".
    /// Must be unique within the batch.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>The user message the child processes — what it is being asked to do.</summary>
    public required string Objective { get; init; }

    /// <summary>
    /// The child's system prompt. When null, a generic focused-delegate prompt is used.
    /// </summary>
    public string? RoleInstructions { get; init; }

    /// <summary>
    /// Working directory for the child as a RELATIVE subpath of the parent's working directory.
    /// Null means the parent's working directory itself. Absolute paths and paths escaping the
    /// parent directory are rejected up front.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// Tool ids the child may see and execute; must be a subset of the parent's registry.
    /// Null inherits every parent tool.
    /// </summary>
    public IReadOnlyList<string>? AllowedTools { get; init; }

    /// <summary>
    /// Key into <see cref="ChildRunOptions.ChildProviders"/> — the parent-defined provider
    /// policy. Null uses the parent's own provider. Children cannot reference providers the
    /// parent did not explicitly offer.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>Per-child turn budget; must not exceed <see cref="ChildRunOptions.MaxTurnsPerChild"/>.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>Per-child wall-clock budget; must not exceed <see cref="ChildRunOptions.MaxDurationPerChild"/>.</summary>
    public TimeSpan? MaxDuration { get; init; }
}

/// <summary>
/// Parent-defined ceilings for a child batch. Defaults are conservative: sequential execution,
/// per-child turn ceiling equal to the parent's own maxTurns, and no aggregate limits.
/// </summary>
public sealed record ChildRunOptions
{
    /// <summary>Maximum children running at once (default 1 — sequential in task order).</summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>Ceiling for any child's MaxTurns; defaults to the parent agent's own maxTurns.</summary>
    public int? MaxTurnsPerChild { get; init; }

    /// <summary>Ceiling for any child's MaxDuration; null means no per-child time ceiling.</summary>
    public TimeSpan? MaxDurationPerChild { get; init; }

    /// <summary>
    /// Aggregate LLM-call budget across the whole batch, enforced race-safely under concurrency.
    /// A child that exhausts it reports <see cref="ChildTaskStatus.BudgetExceeded"/>.
    /// </summary>
    public int? MaxTotalTurns { get; init; }

    /// <summary>Aggregate wall-clock deadline for the whole batch; expiry cancels all children.</summary>
    public TimeSpan? MaxTotalDuration { get; init; }

    /// <summary>
    /// The only provider surface children may reference, keyed by the names tasks use in
    /// <see cref="ChildTask.ProviderName"/>. Null means children always use the parent's provider.
    /// </summary>
    public IReadOnlyDictionary<string, ILlmProvider>? ChildProviders { get; init; }
}

/// <summary>Terminal state of one child task.</summary>
public enum ChildTaskStatus
{
    /// <summary>The child produced a successful final response.</summary>
    Succeeded,

    /// <summary>The child finished unsuccessfully (LLM/tool error or its own max-turns limit).</summary>
    Failed,

    /// <summary>The child was stopped by the aggregate turn budget or its per-child deadline.</summary>
    BudgetExceeded,

    /// <summary>The child was cancelled by the parent token or the batch deadline (possibly before starting).</summary>
    Cancelled,
}

/// <summary>Rollup outcome of a child batch.</summary>
public enum ChildBatchOutcome
{
    /// <summary>Every child succeeded.</summary>
    Succeeded,

    /// <summary>At least one child failed or exceeded a budget; none were cancelled.</summary>
    PartialFailure,

    /// <summary>At least one child was cancelled (parent token or batch deadline).</summary>
    Cancelled,
}

/// <summary>Result of one child task; results are reported for every task, in task-list order.</summary>
public sealed record ChildTaskResult
{
    public required int TaskIndex { get; init; }
    public required string Name { get; init; }
    public required ChildTaskStatus Status { get; init; }

    /// <summary>The child's final response text (or error/cancellation notice).</summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>The underlying SimpleAgent stop reason ("completed", "max_turns_exceeded", "error: ...", "cancelled").</summary>
    public string StopReason { get; init; } = string.Empty;

    /// <summary>LLM calls this child actually dispatched (budget-denied calls are not counted).</summary>
    public int TurnCount { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>Complete report for one <see cref="SimpleAgent.RunChildTasksAsync"/> call.</summary>
public sealed record ChildTaskRunReport
{
    /// <summary>Correlation id shared by every event and result of this batch.</summary>
    public required string ParentRunId { get; init; }

    /// <summary>One result per task, in the same order as the input list — never truncated.</summary>
    public required IReadOnlyList<ChildTaskResult> Results { get; init; }

    public required ChildBatchOutcome Outcome { get; init; }

    /// <summary>Aggregate LLM calls consumed across all children.</summary>
    public required int TotalTurns { get; init; }
}

/// <summary>Kind of a child lifecycle event.</summary>
public enum ChildAgentEventKind
{
    Started,
    ToolCalled,
    Completed,
}

/// <summary>
/// Typed lifecycle/progress event for TUI, headless, and ACP consumers. Events are delivered
/// serialized (never concurrently), correlated by <see cref="ParentRunId"/> + <see cref="ChildName"/>.
/// </summary>
public sealed record ChildAgentEvent
{
    public required string ParentRunId { get; init; }
    public required string ChildName { get; init; }
    public required int TaskIndex { get; init; }
    public required ChildAgentEventKind Kind { get; init; }

    /// <summary>Tool name for <see cref="ChildAgentEventKind.ToolCalled"/>; null otherwise.</summary>
    public string? ToolName { get; init; }

    /// <summary>Terminal status for <see cref="ChildAgentEventKind.Completed"/>; null otherwise.</summary>
    public ChildTaskStatus? Status { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Race-safe aggregate turn budget shared by every child in a batch: each LLM call acquires one
/// turn BEFORE dispatch, so the limit holds exactly under concurrency.
/// </summary>
internal sealed class ChildTurnBudget
{
    private int _remaining;

    public ChildTurnBudget(int totalTurns) => _remaining = totalTurns;

    public bool TryAcquire()
    {
        while (true)
        {
            var current = Volatile.Read(ref _remaining);
            if (current <= 0)
                return false;
            if (Interlocked.CompareExchange(ref _remaining, current - 1, current) == current)
                return true;
        }
    }
}

/// <summary>Thrown by <see cref="BudgetedLlmProvider"/> when the aggregate child turn budget is spent.</summary>
internal sealed class ChildTurnBudgetExceededException : InvalidOperationException
{
    public ChildTurnBudgetExceededException()
        : base("The aggregate turn budget for this child batch is exhausted.")
    {
    }
}

/// <summary>
/// Per-child decorator that charges every LLM call against the shared batch budget (when one is
/// set) and counts the calls actually dispatched, so the coordinator can report exact turn
/// consumption even for children that end in cancellation or budget denial. The
/// <see cref="DeniedByBudget"/> flag is how the coordinator attributes a child's failure to
/// budget exhaustion (SimpleAgent converts the thrown exception into a failed result).
/// </summary>
internal sealed class BudgetedLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private readonly ChildTurnBudget? _budget;
    private int _dispatchedCalls;

    public BudgetedLlmProvider(ILlmProvider inner, ChildTurnBudget? budget)
    {
        _inner = inner;
        _budget = budget;
    }

    public bool DeniedByBudget { get; private set; }

    /// <summary>LLM calls actually admitted and dispatched by this child (denied calls excluded).</summary>
    public int DispatchedCalls => Volatile.Read(ref _dispatchedCalls);

    public string Name => _inner.Name;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        _inner.IsAvailableAsync(cancellationToken);

    public Task<IEnumerable<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default) =>
        _inner.ListModelsAsync(cancellationToken);

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        Charge();
        return _inner.CompleteAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Charge();
        await foreach (var chunk in _inner.StreamCompleteAsync(request, cancellationToken))
            yield return chunk;
    }

    private void Charge()
    {
        if (_budget is not null && !_budget.TryAcquire())
        {
            DeniedByBudget = true;
            throw new ChildTurnBudgetExceededException();
        }
        Interlocked.Increment(ref _dispatchedCalls);
    }
}

/// <summary>
/// Read-only filtered view of the parent registry: a child sees only its allowed tools, and can
/// never mutate the parent's registrations (mutators throw). Keeping disallowed tools out of
/// this view keeps them out of the child's LLM requests entirely; <see cref="ChildToolExecutor"/>
/// covers a model that hallucinates a disallowed name anyway.
/// </summary>
internal sealed class ChildToolRegistry : IToolRegistry
{
    private readonly IToolRegistry _inner;
    private readonly IReadOnlySet<string> _allowedTools;

    public ChildToolRegistry(IToolRegistry inner, IReadOnlySet<string> allowedTools)
    {
        _inner = inner;
        _allowedTools = allowedTools;
    }

    public event EventHandler<ToolRegisteredEventArgs>? ToolRegistered
    {
        add => _inner.ToolRegistered += value;
        remove => _inner.ToolRegistered -= value;
    }

    public event EventHandler<ToolUnregisteredEventArgs>? ToolUnregistered
    {
        add => _inner.ToolUnregistered += value;
        remove => _inner.ToolUnregistered -= value;
    }

    private bool IsAllowed(ToolRegistration registration) =>
        registration.Metadata?.Id is { } id && _allowedTools.Contains(id);

    public IReadOnlyList<ToolRegistration> Tools =>
        _inner.Tools.Where(IsAllowed).ToList();

    public ToolRegistration? GetTool(string toolId) =>
        _allowedTools.Contains(toolId) ? _inner.GetTool(toolId) : null;

    public IReadOnlyList<ToolRegistration> GetTools(
        ToolCategory? category = null,
        ToolCapability? requiredCapabilities = null,
        IEnumerable<string>? tags = null,
        bool enabledOnly = true) =>
        _inner.GetTools(category, requiredCapabilities, tags, enabledOnly).Where(IsAllowed).ToList();

    public IReadOnlyList<ToolRegistration> SearchTools(string searchText, bool enabledOnly = true) =>
        _inner.SearchTools(searchText, enabledOnly).Where(IsAllowed).ToList();

    public ITool CreateTool(string toolId, IServiceProvider serviceProvider) =>
        _allowedTools.Contains(toolId)
            ? _inner.CreateTool(toolId, serviceProvider)
            : throw new InvalidOperationException($"Tool '{toolId}' is not permitted for this child agent.");

    public ToolRegistration RegisterTool<T>(Dictionary<string, object?>? configuration = null)
        where T : class, ITool =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");

    public ToolRegistration RegisterTool(
        ToolMetadata metadata,
        Func<IServiceProvider, ITool> factory,
        Dictionary<string, object?>? configuration = null) =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");

    public ToolRegistration RegisterTool(Type toolType, Dictionary<string, object?>? configuration = null) =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");

    public bool UnregisterTool(string toolId) =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");

    public bool SetToolEnabled(string toolId, bool enabled) =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");

    public bool UpdateToolConfiguration(string toolId, Dictionary<string, object?> configuration) =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");

    public ToolRegistryStatistics GetStatistics() => _inner.GetStatistics();

    public void Clear() =>
        throw new NotSupportedException("A child agent cannot modify the parent tool registry.");
}

/// <summary>
/// Executor guard: any attempt to execute a tool outside the child's allow-set returns a failed
/// tool result (surfaced to the child's model as a normal tool error, so the child can recover)
/// instead of running the tool. Everything else delegates to the parent executor.
/// </summary>
internal sealed class ChildToolExecutor : IToolExecutor
{
    private readonly IToolExecutor _inner;
    private readonly IReadOnlySet<string> _allowedTools;

    public ChildToolExecutor(IToolExecutor inner, IReadOnlySet<string> allowedTools)
    {
        _inner = inner;
        _allowedTools = allowedTools;
    }

    public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted
    {
        add => _inner.ExecutionStarted += value;
        remove => _inner.ExecutionStarted -= value;
    }

    public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted
    {
        add => _inner.ExecutionCompleted += value;
        remove => _inner.ExecutionCompleted -= value;
    }

    public event EventHandler<SecurityViolationEventArgs>? SecurityViolation
    {
        add => _inner.SecurityViolation += value;
        remove => _inner.SecurityViolation -= value;
    }

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request) =>
        _allowedTools.Contains(request.ToolId)
            ? _inner.ExecuteAsync(request)
            : Task.FromResult(Denied(request.ToolId));

    public Task<ToolExecutionResult> ExecuteAsync(
        string toolId,
        Dictionary<string, object?> parameters,
        ToolExecutionContext context) =>
        _allowedTools.Contains(toolId)
            ? _inner.ExecuteAsync(toolId, parameters, context)
            : Task.FromResult(Denied(toolId));

    public Task<IList<string>> ValidateExecutionRequestAsync(ToolExecutionRequest request) =>
        _inner.ValidateExecutionRequestAsync(request);

    public Task<ToolResourceUsage> EstimateResourceUsageAsync(
        string toolId,
        Dictionary<string, object?> parameters) =>
        _inner.EstimateResourceUsageAsync(toolId, parameters);

    public Task<int> CancelExecutionsAsync(string? toolId = null) =>
        _inner.CancelExecutionsAsync(toolId);

    public IReadOnlyList<RunningExecutionInfo> GetRunningExecutions() =>
        _inner.GetRunningExecutions();

    public ToolExecutionStatistics GetStatistics() => _inner.GetStatistics();

    private static ToolExecutionResult Denied(string toolId) => new()
    {
        ToolId = toolId,
        IsSuccessful = false,
        ErrorMessage = $"Tool '{toolId}' is not permitted for this child agent.",
    };
}
