namespace Andy.Engine.Benchmarks.Framework;

/// <summary>
/// Result of a validation step
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Name of the validator
    /// </summary>
    public required string ValidatorName { get; init; }

    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Detailed message
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Additional details (errors, warnings, etc.)
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = new();
}