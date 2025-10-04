using Andy.Benchmarks.Framework;

namespace Andy.Benchmarks.Validators;

/// <summary>
/// Base interface for all validators
/// </summary>
public interface IValidator
{
    /// <summary>
    /// Validates a benchmark result against a scenario
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        BenchmarkScenario scenario,
        BenchmarkResult result,
        CancellationToken cancellationToken = default);
}
