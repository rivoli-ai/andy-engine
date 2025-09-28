using System.Collections.Concurrent;
using Andy.Engine.Contracts;
using Andy.Engine.Critic;
using Andy.Engine.Planner;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.State;

/// <summary>
/// Manages agent state updates and transitions.
/// </summary>
public class StateManager
{
    private readonly IStateStore _stateStore;
    private readonly ILogger<StateManager>? _logger;
    private readonly StateOptions _options;
    private readonly ConcurrentDictionary<Guid, AgentState> _cache;

    public StateManager(
        IStateStore stateStore,
        StateOptions? options = null,
        ILogger<StateManager>? logger = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _options = options ?? StateOptions.Default;
        _logger = logger;
        _cache = new ConcurrentDictionary<Guid, AgentState>();
    }

    /// <summary>
    /// Creates a new agent state for a goal.
    /// </summary>
    public AgentState CreateInitialState(AgentGoal goal, Budget budget)
    {
        return new AgentState(
            Goal: goal,
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: null,
            Budget: budget,
            TurnIndex: 0,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );
    }

    /// <summary>
    /// Updates the state based on a planner decision and observation.
    /// </summary>
    public AgentState UpdateState(
        AgentState currentState,
        PlannerDecision decision,
        Observation? observation,
        Critique? critique)
    {
        _logger?.LogDebug("Updating state for turn {Turn}", currentState.TurnIndex);

        var newSubgoals = currentState.Subgoals.ToList();
        var newWorkingMemory = new Dictionary<string, string>(currentState.WorkingMemoryDigest);
        var newTurnIndex = currentState.TurnIndex + 1;
        ToolCall? newLastAction = null;

        // Update based on decision type
        switch (decision)
        {
            case CallToolDecision toolDecision:
                newLastAction = toolDecision.Call;
                break;

            case ReplanDecision replanDecision:
                newSubgoals = replanDecision.NewSubgoals.ToList();
                UpdateWorkingMemory(newWorkingMemory, "replan", $"New subgoals: {string.Join(", ", newSubgoals)}");
                break;

            case AskUserDecision askDecision:
                UpdateWorkingMemory(newWorkingMemory, "user_query", askDecision.Question);
                break;

            case StopDecision stopDecision:
                UpdateWorkingMemory(newWorkingMemory, "stop_reason", stopDecision.Reason);
                break;
        }

        // Update working memory with observation
        if (observation != null)
        {
            UpdateWorkingMemory(newWorkingMemory, $"turn_{currentState.TurnIndex}_summary", observation.Summary);

            // Add key facts to working memory
            foreach (var fact in observation.KeyFacts.Take(_options.MaxFactsInMemory))
            {
                UpdateWorkingMemory(newWorkingMemory, $"fact_{fact.Key}", fact.Value);
            }
        }

        // Update based on critique
        if (critique != null)
        {
            UpdateWorkingMemory(newWorkingMemory, "critique_assessment", critique.Assessment);
            if (critique.KnownGaps.Any())
            {
                UpdateWorkingMemory(newWorkingMemory, "known_gaps", string.Join(", ", critique.KnownGaps));
            }
        }

        // Compress working memory if it's getting too large
        if (newWorkingMemory.Count > _options.MaxMemoryEntries)
        {
            newWorkingMemory = CompressWorkingMemory(newWorkingMemory);
        }

        return new AgentState(
            Goal: currentState.Goal,
            Subgoals: newSubgoals,
            LastAction: newLastAction,
            LastObservation: observation,
            Budget: currentState.Budget,
            TurnIndex: newTurnIndex,
            WorkingMemoryDigest: newWorkingMemory
        );
    }

    /// <summary>
    /// Saves the state to the store.
    /// </summary>
    public async Task SaveStateAsync(Guid traceId, AgentState state, CancellationToken cancellationToken = default)
    {
        _cache[traceId] = state;
        await _stateStore.SaveAsync(traceId, state, cancellationToken);
        _logger?.LogDebug("Saved state for trace {TraceId}, turn {Turn}", traceId, state.TurnIndex);
    }

    /// <summary>
    /// Loads the state from the store or cache.
    /// </summary>
    public async Task<AgentState?> LoadStateAsync(Guid traceId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(traceId, out var cachedState))
        {
            return cachedState;
        }

        var state = await _stateStore.LoadAsync(traceId, cancellationToken);
        if (state != null)
        {
            _cache[traceId] = state;
        }

        return state;
    }

    /// <summary>
    /// Clears the state from cache and store.
    /// </summary>
    public async Task ClearStateAsync(Guid traceId, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(traceId, out _);
        await _stateStore.DeleteAsync(traceId, cancellationToken);
        _logger?.LogDebug("Cleared state for trace {TraceId}", traceId);
    }

    private void UpdateWorkingMemory(Dictionary<string, string> memory, string key, string value)
    {
        // Limit value length
        if (value.Length > _options.MaxMemoryValueLength)
        {
            value = value.Substring(0, _options.MaxMemoryValueLength - 3) + "...";
        }

        memory[key] = value;
    }

    private Dictionary<string, string> CompressWorkingMemory(Dictionary<string, string> memory)
    {
        _logger?.LogDebug("Compressing working memory from {Count} entries", memory.Count);

        // Keep only the most recent entries and important keys
        var importantKeys = new HashSet<string> { "stop_reason", "critique_assessment", "known_gaps", "user_query" };

        var compressed = new Dictionary<string, string>();

        // Keep important keys
        foreach (var key in importantKeys)
        {
            if (memory.TryGetValue(key, out var value))
            {
                compressed[key] = value;
            }
        }

        // Keep recent turn summaries
        var turnKeys = memory.Keys
            .Where(k => k.StartsWith("turn_"))
            .OrderByDescending(k => k)
            .Take(_options.MaxTurnSummaries);

        foreach (var key in turnKeys)
        {
            compressed[key] = memory[key];
        }

        // Keep recent facts
        var factKeys = memory.Keys
            .Where(k => k.StartsWith("fact_"))
            .OrderByDescending(k => k)
            .Take(_options.MaxFactsInMemory);

        foreach (var key in factKeys)
        {
            compressed[key] = memory[key];
        }

        _logger?.LogDebug("Compressed working memory to {Count} entries", compressed.Count);
        return compressed;
    }
}

/// <summary>
/// Options for state management.
/// </summary>
public class StateOptions
{
    public int MaxMemoryEntries { get; set; } = 50;
    public int MaxMemoryValueLength { get; set; } = 500;
    public int MaxFactsInMemory { get; set; } = 10;
    public int MaxTurnSummaries { get; set; } = 5;

    public static StateOptions Default => new();
}