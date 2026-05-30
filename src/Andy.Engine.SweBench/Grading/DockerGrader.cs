using Andy.Engine.SweBench.Grading.LogParsers;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading;

/// <summary>
/// Grades a single prediction inside its official Docker image: applies the model patch
/// and the official test patch, runs the repo/version test command, parses the log, and
/// computes the resolution status — faithful to the swebench harness.
/// </summary>
public sealed class DockerGrader
{
    private readonly IDockerClient _docker;
    private readonly TestSpecBuilder _specBuilder;
    private readonly LogParserRegistry _parsers;
    private readonly GradingEngine _grading;
    private readonly TimeSpan _timeout;

    public DockerGrader(
        IDockerClient docker,
        TestSpecBuilder? specBuilder = null,
        LogParserRegistry? parsers = null,
        GradingEngine? grading = null,
        TimeSpan? timeout = null)
    {
        _docker = docker;
        _specBuilder = specBuilder ?? new TestSpecBuilder();
        _parsers = parsers ?? new LogParserRegistry();
        _grading = grading ?? new GradingEngine();
        _timeout = timeout ?? TimeSpan.FromMinutes(30);
    }

    public async Task<InstanceGradeResult> GradeAsync(
        SweBenchInstance instance, SwePrediction prediction, CancellationToken cancellationToken = default)
    {
        if (prediction.IsEmptyPatch)
        {
            return new InstanceGradeResult
            {
                InstanceId = instance.InstanceId,
                EmptyPatch = true,
                PatchApplied = false,
                ResolvedStatus = ResolvedStatus.No,
            };
        }

        InstanceTestSpec spec;
        try
        {
            spec = _specBuilder.Build(instance, prediction);
        }
        catch (Exception ex)
        {
            return Error(instance, $"spec build failed: {ex.Message}");
        }

        DockerRunResult run;
        try
        {
            run = await _docker.RunAsync(
                new DockerRunSpec { Image = spec.ImageTag, Script = spec.EvalScript, Timeout = _timeout },
                cancellationToken);
        }
        catch (Exception ex)
        {
            return Error(instance, $"docker run failed: {ex.Message}");
        }

        var log = run.Combined;

        if (run.TimedOut)
            return Error(instance, "test run timed out", timedOut: true, rawLog: log);

        // Model patch failed to apply -> unresolved (a legitimate prediction failure, not an error).
        if (log.Contains(EvalConstants.ApplyPatchFail, StringComparison.Ordinal))
        {
            return new InstanceGradeResult
            {
                InstanceId = instance.InstanceId,
                PatchApplied = false,
                ResolvedStatus = ResolvedStatus.No,
                RawTestLog = log,
            };
        }

        // Harness/env problems.
        if (log.Contains(EvalConstants.ResetFailed, StringComparison.Ordinal))
            return Error(instance, "reset failed", rawLog: log);

        var hasStart = log.Contains(EvalConstants.StartTestOutput, StringComparison.Ordinal);
        var hasEnd = log.Contains(EvalConstants.EndTestOutput, StringComparison.Ordinal);
        if (!hasStart || !hasEnd)
            return Error(instance, "test output markers missing (env/test_patch error)", rawLog: log);

        // Slice the region between the markers (get_logs_eval).
        var testRegion = Between(log, EvalConstants.StartTestOutput, EvalConstants.EndTestOutput);

        if (!_parsers.TryGet(spec.ParserKey, out var parser))
            return Error(instance, $"no log parser for '{spec.ParserKey}'", rawLog: log);

        var statusMap = parser.Parse(testRegion);
        if (statusMap.Count == 0)
            statusMap = parser.Parse(log); // get_logs_eval fallback: retry on full content.

        var resolved = _grading.GetResolutionStatus(instance.FailToPass, instance.PassToPass, statusMap);

        return new InstanceGradeResult
        {
            InstanceId = instance.InstanceId,
            PatchApplied = true,
            TestPatchApplied = true,
            StatusMap = statusMap,
            ResolvedStatus = resolved,
            RawTestLog = log,
        };
    }

    private static string Between(string s, string start, string end)
    {
        var i = s.IndexOf(start, StringComparison.Ordinal);
        if (i < 0) return s;
        i += start.Length;
        var j = s.IndexOf(end, i, StringComparison.Ordinal);
        return j < 0 ? s[i..] : s[i..j];
    }

    private static InstanceGradeResult Error(
        SweBenchInstance instance, string error, bool timedOut = false, string rawLog = "") =>
        new()
        {
            InstanceId = instance.InstanceId,
            Error = error,
            TimedOut = timedOut,
            ResolvedStatus = ResolvedStatus.No,
            RawTestLog = rawLog,
        };
}
