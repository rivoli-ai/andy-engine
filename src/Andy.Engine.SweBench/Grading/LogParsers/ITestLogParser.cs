using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading.LogParsers;

/// <summary>
/// Parses a repo-specific test log into a map of test id -&gt; status.
/// Test ids that never appear in the log are simply absent from the map
/// (the grader treats absence as a failure).
/// </summary>
public interface ITestLogParser
{
    IReadOnlyDictionary<string, TestStatus> Parse(string log);
}
