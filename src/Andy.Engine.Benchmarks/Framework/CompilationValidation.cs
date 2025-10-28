namespace Andy.Benchmarks.Framework;

public class CompilationValidation
{
    /// <summary>
    /// Whether compilation must succeed
    /// </summary>
    public bool MustSucceed { get; init; } = true;

    /// <summary>
    /// Maximum number of warnings allowed
    /// </summary>
    public int? MaxWarnings { get; init; }
}