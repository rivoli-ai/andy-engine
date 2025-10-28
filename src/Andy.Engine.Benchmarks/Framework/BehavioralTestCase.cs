namespace Andy.Benchmarks.Framework;

public class BehavioralTestCase
{
    /// <summary>
    /// Standard input to provide to the application
    /// </summary>
    public string? Stdin { get; init; }

    /// <summary>
    /// Expected patterns in stdout
    /// </summary>
    public List<string> ExpectedStdoutContains { get; init; } = new();

    /// <summary>
    /// Expected exit code
    /// </summary>
    public int ExpectedExitCode { get; init; } = 0;
}