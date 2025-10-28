namespace Andy.Benchmarks.Framework;

/// <summary>
/// Validation for a specific conversation turn
/// </summary>
public class TurnValidation
{
    /// <summary>
    /// Turn number (1-indexed)
    /// </summary>
    public int TurnNumber { get; init; }

    /// <summary>
    /// Strings that must appear in this turn's response
    /// </summary>
    public List<string> MustContain { get; init; } = new();

    /// <summary>
    /// Strings that must NOT appear in this turn's response
    /// </summary>
    public List<string> MustNotContain { get; init; } = new();

    /// <summary>
    /// Minimum response length for this turn
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    /// Whether agent must not ask user for input on this turn
    /// </summary>
    public bool MustNotAskUser { get; init; }
}