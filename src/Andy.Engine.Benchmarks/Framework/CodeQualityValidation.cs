namespace Andy.Benchmarks.Framework;

public class CodeQualityValidation
{
    /// <summary>
    /// Whether to run code formatting validation
    /// </summary>
    public bool RunLinting { get; init; } = true;

    /// <summary>
    /// Maximum cyclomatic complexity allowed
    /// </summary>
    public int? MaxComplexity { get; init; }
}