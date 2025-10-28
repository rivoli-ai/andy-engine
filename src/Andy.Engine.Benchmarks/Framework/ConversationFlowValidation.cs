namespace Andy.Benchmarks.Framework;

/// <summary>
/// Validation for multi-turn conversation flows
/// </summary>
public class ConversationFlowValidation
{
    /// <summary>
    /// Minimum number of conversation turns
    /// </summary>
    public int MinTurns { get; init; } = 1;

    /// <summary>
    /// Maximum number of conversation turns
    /// </summary>
    public int? MaxTurns { get; init; }

    /// <summary>
    /// Whether context must be maintained across turns
    /// </summary>
    public bool MustMaintainContext { get; init; } = true;

    /// <summary>
    /// Validation rules for specific turns
    /// </summary>
    public List<TurnValidation> TurnValidations { get; init; } = new();
}