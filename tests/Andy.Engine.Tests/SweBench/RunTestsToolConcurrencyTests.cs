using System.Diagnostics;
using Andy.Engine.SweBench.Agent;
using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Andy.Engine.Tests.SweBench;

/// <summary>
/// SimpleAgent executes a turn's tool calls concurrently, so RunTestsTool's invocation cap must
/// hold under parallel calls: previously two calls with one use left both passed the
/// check-then-act and both launched Docker runs (issue #41).
/// </summary>
public class RunTestsToolConcurrencyTests : IDisposable
{
    private readonly string _workspace;

    public RunTestsToolConcurrencyTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "andy-run-tests-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspace);
        Git("init", "-q");
        Git("config", "user.email", "test@local");
        Git("config", "user.name", "test");
        File.WriteAllText(Path.Combine(_workspace, "a.py"), "print('hello')\n");
        Git("add", "-A");
        Git("commit", "-q", "-m", "init");
        // An uncommitted edit so CaptureWorkingTreeDiffAsync yields a non-empty diff.
        File.WriteAllText(Path.Combine(_workspace, "a.py"), "print('hello world')\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); } catch { /* best effort */ }
    }

    private void Git(params string[] args)
    {
        var psi = new ProcessStartInfo { FileName = "git", WorkingDirectory = _workspace };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        p.ExitCode.Should().Be(0, $"git {string.Join(' ', args)} must succeed");
    }

    /// <summary>Fake docker that records peak concurrency and returns a passing test log.</summary>
    private sealed class CountingDockerClient : IDockerClient
    {
        private int _inFlight;
        public int TotalRuns;
        public int PeakConcurrency;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> EnsureImageAsync(string image, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public async Task<DockerRunResult> RunAsync(DockerRunSpec spec, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _inFlight);
            InterlockedMax(ref PeakConcurrency, now);
            Interlocked.Increment(ref TotalRuns);
            await Task.Delay(100, cancellationToken);
            Interlocked.Decrement(ref _inFlight);
            return new DockerRunResult { ExitCode = 0, Stdout = "1 passed", Stderr = "" };
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int snapshot;
            while (value > (snapshot = Volatile.Read(ref target)))
                Interlocked.CompareExchange(ref target, value, snapshot);
        }
    }

    private static SweBenchInstance Instance() => new()
    {
        InstanceId = "test__repo-1",
        Repo = "test/repo",
        BaseCommit = "abc",
        ProblemStatement = "fix it",
    };

    [Fact]
    public async Task ConcurrentCalls_NeverExceedTheInvocationCap()
    {
        var docker = new CountingDockerClient();
        var runner = new SweTestRunner(docker);
        var tool = new RunTestsTool(Instance(), _workspace, runner, maxInvocations: 1);
        await tool.InitializeAsync();

        var context = new ToolExecutionContext { WorkingDirectory = _workspace };
        var parameters = new Dictionary<string, object?> { ["test_command"] = "pytest -q" };

        // Two calls in the same turn (SimpleAgent runs them via Task.WhenAll).
        var results = await Task.WhenAll(
            tool.ExecuteAsync(new Dictionary<string, object?>(parameters), context),
            tool.ExecuteAsync(new Dictionary<string, object?>(parameters), context));

        docker.TotalRuns.Should().Be(1, "one use remained, so exactly one docker run may launch");
        results.Count(r => r.IsSuccessful).Should().Be(1);
        results.Count(r => !r.IsSuccessful).Should().Be(1);
        results.Single(r => !r.IsSuccessful).ErrorMessage.Should().Contain("limit reached");
    }

    [Fact]
    public async Task ConcurrentCalls_AreSerialized_InTheSharedWorkspace()
    {
        var docker = new CountingDockerClient();
        var runner = new SweTestRunner(docker);
        var tool = new RunTestsTool(Instance(), _workspace, runner, maxInvocations: 5);
        await tool.InitializeAsync();

        var context = new ToolExecutionContext { WorkingDirectory = _workspace };
        var parameters = new Dictionary<string, object?> { ["test_command"] = "pytest -q" };

        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            tool.ExecuteAsync(new Dictionary<string, object?>(parameters), context)));

        results.Should().OnlyContain(r => r.IsSuccessful);
        docker.TotalRuns.Should().Be(3);
        docker.PeakConcurrency.Should().Be(1,
            "concurrent invocations must serialize: they share the workspace's git index");
    }
}
