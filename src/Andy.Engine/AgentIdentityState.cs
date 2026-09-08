using System.Runtime.InteropServices;
using Andy.Tools.Core;

namespace Andy.Engine;

/// <summary>One logical agent's name and append-only runtime history, independent of conversation clearing.</summary>
public sealed class AgentIdentityState : IAgentIdentity
{
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly Func<AgentActivation> _runtime;
    private string _agentId = Guid.NewGuid().ToString("N");
    private string? _name;
    private AgentActivation _activation;
    private readonly List<AgentIdentityEvent> _history = new();

    public AgentIdentityState(TimeProvider? clock = null, Func<AgentActivation>? runtime = null)
    {
        _clock = clock ?? TimeProvider.System;
        _runtime = runtime ?? (() => new AgentActivation
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            ProcessId = Environment.ProcessId,
            Platform = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            StartedAtUtc = _clock.GetUtcNow()
        });
        _activation = NewActivation();
        Append("created", null);
    }

    public AgentIdentitySnapshot GetSnapshot()
    {
        lock (_gate) return Snapshot();
    }

    public AgentIdentitySnapshot SetName(string? name)
    {
        name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (name is not null && (name.Length > 256 || name.Any(char.IsControl)))
            throw new ArgumentException("Agent name must be at most 256 characters without control characters.", nameof(name));
        lock (_gate)
        {
            if (string.Equals(_name, name, StringComparison.Ordinal)) return Snapshot();
            var previous = _name;
            _name = name;
            Append(name is null ? "cleared" : "renamed", previous);
            return Snapshot();
        }
    }

    /// <summary>Restore portable identity, retaining history and recording a fresh current-host activation.</summary>
    public void Restore(AgentIdentitySnapshot snapshot)
    {
        Validate(snapshot);
        // Obtain runtime metadata before changing any state, so a failing host provider is atomic.
        var activation = NewActivation();
        var events = snapshot.History.ToArray();
        lock (_gate)
        {
            _agentId = snapshot.AgentId;
            _name = snapshot.Name;
            _activation = activation;
            _history.Clear();
            _history.AddRange(events);
            Append("resumed", snapshot.Name);
        }
    }

    internal static void Validate(AgentIdentitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != AgentIdentitySnapshot.CurrentVersion || string.IsNullOrWhiteSpace(snapshot.AgentId)
            || snapshot.Activation is null || snapshot.History is null || snapshot.History.Count == 0)
            throw new ArgumentException("Invalid or unsupported agent identity snapshot.", nameof(snapshot));
        if (snapshot.Name is not null && (snapshot.Name.Length > 256 || snapshot.Name.Any(char.IsControl)))
            throw new ArgumentException("Invalid agent name in identity snapshot.", nameof(snapshot));
        long sequence = 0;
        foreach (var entry in snapshot.History)
        {
            if (entry is null || entry.Sequence != sequence + 1 || entry.Activation is null
                || string.IsNullOrWhiteSpace(entry.Activation.InstanceId) || string.IsNullOrWhiteSpace(entry.Kind))
                throw new ArgumentException("Invalid agent identity history.", nameof(snapshot));
            sequence = entry.Sequence;
        }
        if (snapshot.History[^1].Name != snapshot.Name || snapshot.History[^1].Activation != snapshot.Activation)
            throw new ArgumentException("Agent identity does not match the latest history entry.", nameof(snapshot));
    }

    private AgentActivation NewActivation()
    {
        var observed = _runtime() ?? throw new InvalidOperationException("Runtime provider returned no activation.");
        // A caller may reuse a metadata template. Instance identity is always fresh on activation.
        return observed with { InstanceId = Guid.NewGuid().ToString("N"), StartedAtUtc = _clock.GetUtcNow() };
    }

    private void Append(string kind, string? previous) => _history.Add(new AgentIdentityEvent
    {
        Sequence = _history.Count + 1L,
        Kind = kind,
        PreviousName = previous,
        Name = _name,
        Activation = _activation,
        TimestampUtc = _clock.GetUtcNow()
    });

    private AgentIdentitySnapshot Snapshot() => new()
    {
        AgentId = _agentId,
        Name = _name,
        Activation = _activation,
        History = Array.AsReadOnly(_history.ToArray())
    };
}
