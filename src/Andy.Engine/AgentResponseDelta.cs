namespace Andy.Engine;

/// <summary>
/// Incremental response-streaming event delivered to the optional delta callback of
/// <see cref="SimpleAgent.ProcessMessageAsync(string, Action{AgentResponseDelta}?, CancellationToken)"/>.
///
/// Text deltas are forwarded live, tagged with the loop turn that produced them. Whether a turn's
/// text is the final answer is only knowable once that turn's LLM stream ends; when a turn turns
/// out to be a tool round (or was cut off by the output limit), a single
/// <see cref="AgentResponseDeltaKind.Discarded"/> event is emitted for that turn and its text must
/// be treated as intermediate narration, not final response text. The turn the returned
/// <see cref="SimpleAgentResult"/> is built from is never discarded.
/// </summary>
public sealed record AgentResponseDelta
{
    /// <summary>1-based loop turn (LLM call) this event belongs to.</summary>
    public required int Turn { get; init; }

    /// <summary>What this event conveys.</summary>
    public required AgentResponseDeltaKind Kind { get; init; }

    /// <summary>The text fragment for <see cref="AgentResponseDeltaKind.Text"/>; empty otherwise.</summary>
    public string Text { get; init; } = string.Empty;

    public static AgentResponseDelta TextChunk(int turn, string text) =>
        new() { Turn = turn, Kind = AgentResponseDeltaKind.Text, Text = text };

    public static AgentResponseDelta DiscardedTurn(int turn) =>
        new() { Turn = turn, Kind = AgentResponseDeltaKind.Discarded };
}

/// <summary>Kind of <see cref="AgentResponseDelta"/> event.</summary>
public enum AgentResponseDeltaKind
{
    /// <summary>An ordered fragment of the assistant's text for the tagged turn.</summary>
    Text,

    /// <summary>
    /// The tagged turn ended in tool calls or an output-limit truncation: its previously streamed
    /// text was narration, not the final answer.
    /// </summary>
    Discarded,
}
