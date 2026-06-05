using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Model;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// Tests for the in-loop test runner. A fake IDockerClient captures the script and returns canned
/// output, so these need no Docker. Key property under test: the runner never injects the instance's
/// test_patch (leakage safety), and it classifies apply-fail / timeout / normal output correctly.
/// </summary>
public class SweTestRunnerTests
{
    private sealed class FakeDocker : IDockerClient
    {
        private readonly DockerRunResult _result;
        public DockerRunSpec? LastSpec { get; private set; }
        public FakeDocker(DockerRunResult result) => _result = result;
        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnsureImageAsync(string image, CancellationToken ct = default) => Task.FromResult(true);
        public Task<DockerRunResult> RunAsync(DockerRunSpec spec, CancellationToken ct = default)
        {
            LastSpec = spec;
            return Task.FromResult(_result);
        }
    }

    private static SweBenchInstance Inst() => new()
    {
        InstanceId = "django__django-12193",
        Repo = "django/django",
        BaseCommit = "abc",
        ProblemStatement = "bug",
        TestPatch = "diff --git a/tests/SECRET_TEST.py b/tests/SECRET_TEST.py\n+def test_hidden(): assert False\n",
    };

    private static string Marked(string body) =>
        $"{EvalConstants.ApplyPatchPass}\n{EvalConstants.StartTestOutput}\n{body}\n{EvalConstants.EndTestOutput}\n";

    [Fact]
    public async Task NormalRun_ReturnsOutputBetweenMarkers()
    {
        var docker = new FakeDocker(new DockerRunResult { Stdout = Marked("2 passed, 0 failed") });
        var runner = new SweTestRunner(docker);

        var r = await runner.RunAsync(Inst(), "diff --git a/x.py b/x.py\n", "pytest tests/x.py");

        Assert.True(r.PatchApplied);
        Assert.False(r.TimedOut);
        Assert.Contains("2 passed", r.Output);
        Assert.DoesNotContain(EvalConstants.StartTestOutput, r.Output); // markers stripped
    }

    [Fact]
    public async Task NeverInjectsTestPatch_LeakageSafe()
    {
        var docker = new FakeDocker(new DockerRunResult { Stdout = Marked("ok") });
        var runner = new SweTestRunner(docker);

        await runner.RunAsync(Inst(), "diff --git a/x.py b/x.py\n", "pytest");

        // The generated script must NOT contain the test_patch content or its filename — the hidden
        // tests must be physically absent from the container.
        Assert.NotNull(docker.LastSpec);
        Assert.DoesNotContain("SECRET_TEST", docker.LastSpec!.Script);
        Assert.DoesNotContain("test_hidden", docker.LastSpec!.Script);
        Assert.DoesNotContain(EvalConstants.TestPatchHeredoc, docker.LastSpec!.Script);
    }

    [Fact]
    public async Task ApplyFailure_IsReported()
    {
        var docker = new FakeDocker(new DockerRunResult { Stdout = $"{EvalConstants.ApplyPatchFail}\n" });
        var runner = new SweTestRunner(docker);

        var r = await runner.RunAsync(Inst(), "bad diff", "pytest");

        Assert.False(r.PatchApplied);
        Assert.Contains("did not apply", r.Error);
    }

    [Fact]
    public async Task Timeout_IsReported()
    {
        var docker = new FakeDocker(new DockerRunResult { TimedOut = true });
        var runner = new SweTestRunner(docker);

        var r = await runner.RunAsync(Inst(), "diff", "pytest");

        Assert.True(r.TimedOut);
        Assert.Contains("timed out", r.Error);
    }

    [Fact]
    public async Task EmptyCommand_Rejected()
    {
        var docker = new FakeDocker(new DockerRunResult { Stdout = "" });
        var r = await new SweTestRunner(docker).RunAsync(Inst(), "diff", "   ");
        Assert.Contains("no test command", r.Error);
    }

    [Fact]
    public async Task LargeOutput_Truncated()
    {
        var big = new string('x', 50_000);
        var docker = new FakeDocker(new DockerRunResult { Stdout = Marked(big) });
        var runner = new SweTestRunner(docker, maxOutputChars: 1000);

        var r = await runner.RunAsync(Inst(), "diff", "pytest");

        Assert.True(r.Output.Length < 2000);
        Assert.Contains("truncated", r.Output);
    }

    [Fact]
    public void BuildScript_AppliesCurrentDiff_NoResetNoTestPatch()
    {
        var script = SweTestRunner.BuildScript("diff --git a/y.py b/y.py\n", "pytest tests/y.py");
        Assert.Contains("git apply", script);
        Assert.Contains("pytest tests/y.py", script);
        Assert.Contains(EvalConstants.StartTestOutput, script);
        // No reset-to-base (we run the agent's edits as-is).
        Assert.DoesNotContain("git checkout", script);
    }
}
