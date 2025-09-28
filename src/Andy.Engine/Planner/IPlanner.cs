using Andy.Engine.Contracts;

namespace Andy.Engine.Planner;

/// <summary>
/// Interface for the planner component that decides the next action.
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Decides the next action based on the current agent state.
    /// </summary>
    /// <param name="state">Current agent state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The planner's decision.</returns>
    Task<PlannerDecision> DecideAsync(AgentState state, CancellationToken cancellationToken = default);
}