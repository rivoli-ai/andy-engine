using Andy.Engine.Contracts;

namespace Andy.Engine.State;

/// <summary>
/// Interface for persisting and retrieving agent state.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Loads the agent state for a given trace ID.
    /// </summary>
    /// <param name="traceId">The trace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent state, or null if not found.</returns>
    Task<AgentState?> LoadAsync(Guid traceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the agent state.
    /// </summary>
    /// <param name="traceId">The trace ID.</param>
    /// <param name="state">The state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(Guid traceId, AgentState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the agent state for a given trace ID.
    /// </summary>
    /// <param name="traceId">The trace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid traceId, CancellationToken cancellationToken = default);
}