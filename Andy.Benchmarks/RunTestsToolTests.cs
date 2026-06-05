using System.Diagnostics;
using Andy.Engine.SweBench.Agent;
using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Model;
using Andy.Tools.Core;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// Tests for the in-loop run_tests tool. Uses a real SweTestRunner backed by a fake IDockerClient
/// (no Docker) and a real temp git workspace (for the working-tree diff capture).
/// </summary>
public class RunTestsToolTests
{
    private sealed class FakeDocker : IDockerClient
    {
        private readonly string _stdout;
        public FakeDocker(string stdout) => _stdout = stdout;
        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnsureImageAsync(string image, CancellationToken ct = default) => Task.FromResult(true);
        public Task<DockerRunResult> RunAsync(DockerRunSpec spec, CancellationToken ct = default) =>
            Task.FromResult(new DockerRunResult { Stdout = _stdout });
    }

    private static SweBenchInstance Inst() => new()
    {
        InstanceId = "django__django-12193",
        Repo = "django/django",
        BaseCommit = "abc",
        ProblemStatement = "bug",
    };

    private static void Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo { FileName = "git", WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        Process.Start(psi)!.WaitForExit();
    }

    /// <summary>A temp git repo with one committed file, then an uncommitted edit (a working diff).</summary>
    private static string MakeDirtyRepo()
    {
        var dir = Directory.CreateTempSubdirectory("swe-tool-").FullName;
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@t");
        Git(dir, "config", "user.name", "t");
        File.WriteAllText(Path.Combine(dir, "x.py"), "def f():\n    return 1\n");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-qm", "base");
        File.WriteAllText(Path.Combine(dir, "x.py"), "def f():\n    return 2\n"); // the edit
        return dir;
    }

    private static string Marked(string body) =>
        $"{EvalConstants.ApplyPatchPass}\n{EvalConstants.StartTestOutput}\n{body}\n{EvalConstants.EndTestOutput}\n";

    private static async Task<RunTestsTool> NewTool(string dir, string dockerStdout, int max)
    {
        var tool = new RunTestsTool(Inst(), dir, new SweTestRunner(new FakeDocker(dockerStdout)), max);
        await tool.InitializeAsync(null, CancellationToken.None);
        return tool;
    }

    private static Task<ToolResult> Run(RunTestsTool tool, string cmd) =>
        tool.ExecuteAsync(
            new Dictionary<string, object?> { ["test_command"] = cmd },
            new ToolExecutionContext { CancellationToken = CancellationToken.None });

    [Fact]
    public async Task EditedWorkspace_RunsAndReturnsOutput()
    {
        var dir = MakeDirtyRepo();
        try
        {
            var tool = await NewTool(dir, Marked("1 passed"), max: 3);

            var r = await Run(tool, "pytest x.py");

            Assert.True(r.IsSuccessful);
            Assert.Contains("1 passed", r.Data?.ToString() ?? r.Message ?? string.Empty);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NoEdits_TellsAgentToEditFirst()
    {
        var dir = Directory.CreateTempSubdirectory("swe-tool-").FullName;
        Git(dir, "init", "-q"); Git(dir, "config", "user.email", "t@t"); Git(dir, "config", "user.name", "t");
        File.WriteAllText(Path.Combine(dir, "x.py"), "x=1\n");
        Git(dir, "add", "-A"); Git(dir, "commit", "-qm", "base"); // clean tree, no working diff
        try
        {
            var tool = await NewTool(dir, Marked("ok"), 3);
            var r = await Run(tool, "pytest");
            Assert.False(r.IsSuccessful);
            Assert.Contains("not edited", r.ErrorMessage ?? r.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task InvocationCap_Enforced()
    {
        var dir = MakeDirtyRepo();
        try
        {
            var tool = await NewTool(dir, Marked("ok"), max: 1);
            var first = await Run(tool, "pytest x.py");
            var second = await Run(tool, "pytest x.py");
            Assert.True(first.IsSuccessful);
            Assert.False(second.IsSuccessful);
            Assert.Contains("limit reached", second.ErrorMessage ?? second.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EmptyCommand_Rejected()
    {
        var dir = MakeDirtyRepo();
        try
        {
            var tool = await NewTool(dir, Marked("ok"), 3);
            var r = await Run(tool, "   ");
            Assert.False(r.IsSuccessful);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Metadata_DeclaresNoProcessExecutionCapability()
    {
        var tool = new RunTestsTool(Inst(), "/tmp", new SweTestRunner(new FakeDocker("")), 3);
        Assert.Equal("run_tests", tool.Metadata.Id);
        // Must not require ProcessExecution, or the files-only agent permissions would block it.
        Assert.False(tool.Metadata.RequiredCapabilities.HasFlag(ToolCapability.ProcessExecution));
    }
}
