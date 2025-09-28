using System.Diagnostics;
using Andy.Engine.Contracts;
using Andy.Engine.Critic;
using Andy.Engine.Executor;
using Andy.Engine.Normalizer;
using Andy.Engine.Planner;
using Andy.Engine.Policy;
using Andy.Engine.State;
using Microsoft.Extensions.Logging;

namespace Andy.Engine;

/// <summary>
/// Main agent orchestrator that coordinates all components.
/// </summary>
public class Agent
{
    private readonly IPlanner _planner;
    private readonly IExecutor _executor;
    private readonly ICritic _critic;
    private readonly IObservationNormalizer _normalizer;
    private readonly PolicyEngine _policyEngine;
    private readonly StateManager _stateManager;
    private readonly ILogger<Agent>? _logger;

    public Agent(
        IPlanner planner,
        IExecutor executor,
        ICritic critic,
        IObservationNormalizer normalizer,
        PolicyEngine policyEngine,
        StateManager stateManager,
        ILogger<Agent>? logger = null)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _critic = critic ?? throw new ArgumentNullException(nameof(critic));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger;
    }

    /// <summary>
    /// Event raised when a turn starts.
    /// </summary>
    public event EventHandler<TurnStartedEventArgs>? TurnStarted;

    /// <summary>
    /// Event raised when a turn completes.
    /// </summary>
    public event EventHandler<TurnCompletedEventArgs>? TurnCompleted;

    /// <summary>
    /// Event raised when a tool is called.
    /// </summary>
    public event EventHandler<ToolCalledEventArgs>? ToolCalled;

    /// <summary>
    /// Event raised when the agent needs user input.
    /// </summary>
    public event EventHandler<UserInputRequestedEventArgs>? UserInputRequested;

    /// <summary>
    /// Runs the agent to achieve a goal.
    /// </summary>
    public async Task<AgentResult> RunAsync(
        AgentGoal goal,
        Budget budget,
        ErrorHandlingPolicy errorPolicy,
        CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        var state = _stateManager.CreateInitialState(
            goal with { CreatedAt = startTime },
            budget
        );

        _logger?.LogInformation("Starting agent run {TraceId} for goal: {Goal}", traceId, goal.UserGoal);

        try
        {
            while (!budget.Exhausted(state.TurnIndex, startTime) && !cancellationToken.IsCancellationRequested)
            {
                var turnResult = await ExecuteTurnAsync(
                    traceId,
                    state,
                    errorPolicy,
                    cancellationToken
                );

                state = turnResult.NewState;
                await _stateManager.SaveStateAsync(traceId, state, cancellationToken);

                if (turnResult.ShouldStop)
                {
                    _logger?.LogInformation("Agent stopping: {Reason}", turnResult.StopReason);
                    return new AgentResult(
                        Success: turnResult.GoalAchieved,
                        FinalState: state,
                        StopReason: turnResult.StopReason ?? "Unknown",
                        TotalTurns: state.TurnIndex,
                        Duration: DateTime.UtcNow - startTime
                    );
                }
            }

            // Budget exhausted or cancelled
            var reason = cancellationToken.IsCancellationRequested
                ? "Cancelled by user"
                : "Budget exhausted";

            return new AgentResult(
                Success: false,
                FinalState: state,
                StopReason: reason,
                TotalTurns: state.TurnIndex,
                Duration: DateTime.UtcNow - startTime
            );
        }
        finally
        {
            await _stateManager.ClearStateAsync(traceId, cancellationToken);
        }
    }

    private async Task<TurnResult> ExecuteTurnAsync(
        Guid traceId,
        AgentState state,
        ErrorHandlingPolicy errorPolicy,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var turnNumber = state.TurnIndex + 1;

        _logger?.LogDebug("Starting turn {Turn} for trace {TraceId}", turnNumber, traceId);
        TurnStarted?.Invoke(this, new TurnStartedEventArgs(traceId, turnNumber));

        try
        {
            // 1. Planner decides next action
            var decision = await _planner.DecideAsync(state, cancellationToken);
            _logger?.LogDebug("Planner decision: {DecisionType}", decision.Type);

            // 2. Policy engine resolves decision into action
            var action = _policyEngine.Resolve(decision, state.LastObservation, errorPolicy, state);

            // 3. Execute action and get observation
            Observation? observation = null;
            Critique? critique = null;

            switch (action)
            {
                case CallToolAction toolAction:
                    observation = await ExecuteToolAsync(toolAction, cancellationToken);
                    ToolCalled?.Invoke(this, new ToolCalledEventArgs(
                        traceId,
                        toolAction.Call.ToolName,
                        observation.Summary
                    ));
                    break;

                case AskUserAction askAction:
                    UserInputRequested?.Invoke(this, new UserInputRequestedEventArgs(
                        traceId,
                        askAction.Question,
                        askAction.MissingFields
                    ));
                    // In a real implementation, we'd wait for user response here
                    return new TurnResult(
                        NewState: state,
                        ShouldStop: true,
                        StopReason: "User input required",
                        GoalAchieved: false
                    );

                case StopAction stopAction:
                    return new TurnResult(
                        NewState: state,
                        ShouldStop: true,
                        StopReason: stopAction.Reason,
                        GoalAchieved: stopAction.Reason.Contains("achieved", StringComparison.OrdinalIgnoreCase)
                    );

                case ReplanAction replanAction:
                    state = state with { Subgoals = replanAction.NewSubgoals };
                    break;
            }

            // 4. Critic assesses observation
            if (observation != null)
            {
                critique = await _critic.AssessAsync(state.Goal, observation, cancellationToken);
                _logger?.LogDebug("Critic assessment: {Assessment}", critique.Assessment);

                if (critique.GoalSatisfied)
                {
                    return new TurnResult(
                        NewState: state,
                        ShouldStop: true,
                        StopReason: "Goal achieved",
                        GoalAchieved: true
                    );
                }
            }

            // 5. Update state
            var newState = _stateManager.UpdateState(state, decision, observation, critique);

            sw.Stop();
            TurnCompleted?.Invoke(this, new TurnCompletedEventArgs(
                traceId,
                turnNumber,
                sw.Elapsed,
                action.Type.ToString()
            ));

            // Check if critic recommends stopping
            if (critique?.Recommendation == CritiqueRecommendation.Stop)
            {
                return new TurnResult(
                    NewState: newState,
                    ShouldStop: true,
                    StopReason: critique.Assessment,
                    GoalAchieved: critique.GoalSatisfied
                );
            }

            return new TurnResult(
                NewState: newState,
                ShouldStop: false,
                StopReason: null,
                GoalAchieved: false
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in turn {Turn}", turnNumber);
            sw.Stop();

            TurnCompleted?.Invoke(this, new TurnCompletedEventArgs(
                traceId,
                turnNumber,
                sw.Elapsed,
                "Error"
            ));

            return new TurnResult(
                NewState: state,
                ShouldStop: true,
                StopReason: $"Error: {ex.Message}",
                GoalAchieved: false
            );
        }
    }

    private async Task<Observation> ExecuteToolAsync(CallToolAction action, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Executing tool {Tool} (attempt {Attempt})",
            action.Call.ToolName, action.RetryAttempt);

        var result = await _executor.ExecuteAsync(action.Call, cancellationToken);

        return _normalizer.Normalize(
            action.Call.ToolName,
            result.Data,
            result
        );
    }

    private record TurnResult(
        AgentState NewState,
        bool ShouldStop,
        string? StopReason,
        bool GoalAchieved
    );
}

/// <summary>
/// Result of an agent run.
/// </summary>
public sealed record AgentResult(
    bool Success,
    AgentState FinalState,
    string StopReason,
    int TotalTurns,
    TimeSpan Duration
);

// Event args classes
public class TurnStartedEventArgs : EventArgs
{
    public Guid TraceId { get; }
    public int TurnNumber { get; }

    public TurnStartedEventArgs(Guid traceId, int turnNumber)
    {
        TraceId = traceId;
        TurnNumber = turnNumber;
    }
}

public class TurnCompletedEventArgs : EventArgs
{
    public Guid TraceId { get; }
    public int TurnNumber { get; }
    public TimeSpan Duration { get; }
    public string ActionType { get; }

    public TurnCompletedEventArgs(Guid traceId, int turnNumber, TimeSpan duration, string actionType)
    {
        TraceId = traceId;
        TurnNumber = turnNumber;
        Duration = duration;
        ActionType = actionType;
    }
}

public class ToolCalledEventArgs : EventArgs
{
    public Guid TraceId { get; }
    public string ToolName { get; }
    public string Result { get; }

    public ToolCalledEventArgs(Guid traceId, string toolName, string result)
    {
        TraceId = traceId;
        ToolName = toolName;
        Result = result;
    }
}

public class UserInputRequestedEventArgs : EventArgs
{
    public Guid TraceId { get; }
    public string Question { get; }
    public IReadOnlyList<string> MissingFields { get; }

    public UserInputRequestedEventArgs(Guid traceId, string question, IReadOnlyList<string> missingFields)
    {
        TraceId = traceId;
        Question = question;
        MissingFields = missingFields;
    }
}