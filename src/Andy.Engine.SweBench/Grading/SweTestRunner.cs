using System.Text;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading;

/// <summary>Outcome of an in-loop test run against the agent's current edits.</summary>
public sealed record TestRunResult
{
    /// <summary>The agent's working-tree diff applied cleanly onto the image's repo.</summary>
    public bool PatchApplied { get; init; }
    public bool TimedOut { get; init; }

    /// <summary>The captured test output (between markers), truncated. Empty if the patch failed to apply.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>A setup/Docker error (not a test failure); null on a normal run.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Runs an agent-chosen test command inside the official per-instance Docker image, against the
/// agent's CURRENT working-tree edits — the in-loop counterpart to <see cref="DockerGrader"/>.
///
/// Leakage-safe by construction: this never injects the instance's <c>test_patch</c>, so the hidden
/// FAIL_TO_PASS tests (which that patch adds) do not exist in the container. The agent can only run
/// the repo's existing tests and any reproduction test it has written into its own working tree.
/// </summary>
public sealed class SweTestRunner
{
    private readonly IDockerClient _docker;
    private readonly TestSpecBuilder _specBuilder;
    private readonly TimeSpan _timeout;
    private readonly int _maxOutputChars;

    public SweTestRunner(
        IDockerClient docker,
        TestSpecBuilder? specBuilder = null,
        TimeSpan? timeout = null,
        int maxOutputChars = 20_000)
    {
        _docker = docker;
        _specBuilder = specBuilder ?? new TestSpecBuilder();
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
        _maxOutputChars = maxOutputChars;
    }

    public async Task<TestRunResult> RunAsync(
        SweBenchInstance instance, string currentDiff, string testCommand, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(testCommand))
            return new TestRunResult { Error = "no test command given" };

        var image = _specBuilder.GetImageTag(instance.InstanceId);
        var script = BuildScript(currentDiff, testCommand);

        DockerRunResult run;
        try
        {
            run = await _docker.RunAsync(
                new DockerRunSpec { Image = image, Script = script, Timeout = _timeout }, cancellationToken);
        }
        catch (Exception ex)
        {
            return new TestRunResult { Error = $"docker run failed: {ex.Message}" };
        }

        if (run.TimedOut)
            return new TestRunResult { TimedOut = true, Error = "test run timed out" };

        var log = run.Combined;

        if (log.Contains(EvalConstants.ApplyPatchFail, StringComparison.Ordinal))
            return new TestRunResult { PatchApplied = false, Error = "your current edits did not apply cleanly onto a fresh checkout" };

        var hasStart = log.Contains(EvalConstants.StartTestOutput, StringComparison.Ordinal);
        var hasEnd = log.Contains(EvalConstants.EndTestOutput, StringComparison.Ordinal);
        var region = hasStart && hasEnd
            ? Between(log, EvalConstants.StartTestOutput, EvalConstants.EndTestOutput)
            : log; // fall back to the whole log if markers are missing (e.g. the command crashed early)

        return new TestRunResult
        {
            PatchApplied = true,
            Output = Truncate(region.Trim(), _maxOutputChars),
        };
    }

    /// <summary>
    /// Builds the in-loop script: apply the agent's current diff (injection-proof base64 embedding +
    /// the same fallback chain as the grader), activate the env, run the agent's test command between
    /// markers. No test_patch, no reset — we run the agent's edits as-is.
    /// </summary>
    internal static string BuildScript(string currentDiff, string testCommand)
    {
        var sb = new StringBuilder();
        sb.Append("#!/bin/bash\n");
        sb.Append("set -uxo pipefail\n");
        sb.Append($"cd {EvalConstants.RepoDir}\n");

        TestSpecBuilder.AppendModelPatchDecode(sb, currentDiff);
        sb.Append("__APPLIED=0\n");
        foreach (var cmd in EvalConstants.GitApplyCmds)
            sb.Append($"if [ \"$__APPLIED\" != \"1\" ]; then {cmd} /tmp/model_patch.diff && __APPLIED=1; fi\n");
        sb.Append($"if [ \"$__APPLIED\" != \"1\" ]; then echo \"{EvalConstants.ApplyPatchFail}\"; exit 0; fi\n");

        sb.Append("source /opt/miniconda3/bin/activate\n");
        sb.Append($"conda activate {EvalConstants.CondaEnv}\n");
        sb.Append($"git config --global --add safe.directory {EvalConstants.RepoDir}\n");
        sb.Append($"cd {EvalConstants.RepoDir}\n");

        sb.Append($": '{EvalConstants.StartTestOutput}'\n");
        sb.Append(testCommand).Append('\n');
        sb.Append($": '{EvalConstants.EndTestOutput}'\n");

        return sb.ToString();
    }

    private static string Between(string s, string start, string end)
    {
        var i = s.IndexOf(start, StringComparison.Ordinal);
        if (i < 0) return s;
        i += start.Length;
        var j = s.IndexOf(end, i, StringComparison.Ordinal);
        return j < 0 ? s[i..] : s[i..j];
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + $"\n... [truncated {s.Length - max} chars]";
}
