using Andy.Engine.Contracts;

namespace Andy.Engine.Critic;

/// <summary>
/// Interface for the critic component that evaluates observations against goals.
/// </summary>
public interface ICritic
{
    /// <summary>
    /// Assesses whether an observation satisfies the active goal/subgoal.
    /// </summary>
    /// <param name="goal">The goal to assess against.</param>
    /// <param name="observation">The observation to assess.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The critique result.</returns>
    Task<Critique> AssessAsync(AgentGoal goal, Observation observation, CancellationToken cancellationToken = default);
}