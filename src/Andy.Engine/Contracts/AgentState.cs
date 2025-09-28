namespace Andy.Engine.Contracts;

/// <summary>
/// Core agent state and related types.
/// </summary>
public sealed record AgentGoal(
    string UserGoal,
    IReadOnlyList<string> Constraints,
    DateTime? CreatedAt = null
);

public sealed record Budget(int MaxTurns, TimeSpan MaxWallClock)
{
    public bool Exhausted(int currentTurn, DateTime startTime) =>
        currentTurn >= MaxTurns || DateTime.UtcNow - startTime >= MaxWallClock;
}

public sealed record AgentState(
    AgentGoal Goal,
    IReadOnlyList<string> Subgoals,
    ToolCall? LastAction,
    Observation? LastObservation,
    Budget Budget,
    int TurnIndex,
    IReadOnlyDictionary<string, string> WorkingMemoryDigest
);

public sealed record ErrorHandlingPolicy(
    int MaxRetries,
    TimeSpan BaseBackoff,
    bool UseFallbacks,
    bool AskUserWhenMissingFields
);