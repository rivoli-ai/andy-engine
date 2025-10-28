namespace Andy.Benchmarks.Framework;

public class BehavioralValidation
{
    /// <summary>
    /// Type of behavioral validation: "unit-tests", "console-app", "web-app"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Command to run the application (for console-app/web-app)
    /// </summary>
    public string? RunCommand { get; init; }

    /// <summary>
    /// Test cases with inputs and expected outputs
    /// </summary>
    public List<BehavioralTestCase> TestCases { get; init; } = new();
}