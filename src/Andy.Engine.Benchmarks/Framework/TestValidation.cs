namespace Andy.Benchmarks.Framework;

public class TestValidation
{
    /// <summary>
    /// Whether all tests must pass
    /// </summary>
    public bool MustPass { get; init; } = true;

    /// <summary>
    /// Minimum code coverage percentage required
    /// </summary>
    public double? MinCoverage { get; init; }

    /// <summary>
    /// Specific test that must pass (optional)
    /// </summary>
    public string? SpecificTest { get; init; }
}