namespace Andy.Engine.SweBench.Model;

/// <summary>
/// Per-test outcome parsed from a test log. Mirrors the statuses the official
/// harness recognizes. A test id that is ABSENT from the parsed log is treated
/// as a failure by the grader (it is never represented as a TestStatus value).
/// </summary>
public enum TestStatus
{
    Passed,
    Failed,
    Error,
    Skipped,
    XFail,
}
