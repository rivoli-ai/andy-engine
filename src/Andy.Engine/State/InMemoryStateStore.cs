using System.Collections.Concurrent;
using System.Text.Json;
using Andy.Engine.Contracts;

namespace Andy.Engine.State;

/// <summary>
/// In-memory implementation of state store for development and testing.
/// </summary>
public class InMemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<Guid, string> _states = new();

    public Task<AgentState?> LoadAsync(Guid traceId, CancellationToken cancellationToken = default)
    {
        if (_states.TryGetValue(traceId, out var json))
        {
            var state = JsonSerializer.Deserialize<AgentState>(json);
            return Task.FromResult(state);
        }

        return Task.FromResult<AgentState?>(null);
    }

    public Task SaveAsync(Guid traceId, AgentState state, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(state);
        _states[traceId] = json;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid traceId, CancellationToken cancellationToken = default)
    {
        _states.TryRemove(traceId, out _);
        return Task.CompletedTask;
    }
}