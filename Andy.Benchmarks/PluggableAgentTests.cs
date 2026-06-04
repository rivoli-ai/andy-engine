using System.Runtime.InteropServices;
using Andy.Engine.SweBench.Agent;
using Andy.Engine.SweBench.Orchestration;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// Tests for the pluggable-agent seam: the external CLI agent edits a workspace and reports
/// outcomes, the factory selector maps --agent to the right implementation, and the CLI parses
/// the new flags. These need neither Docker nor an LLM. The external-agent tests shell out to a
/// tiny POSIX script, so they are skipped on Windows.
/// </summary>
public class PluggableAgentTests
{
    private static bool IsPosix => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>Creates a temp workspace dir and a temp shell script; returns (workspace, scriptPath).</summary>
    private static (string workspace, string script) MakeScript(string body)
    {
        var workspace = Directory.CreateTempSubdirectory("swe-ws-").FullName;
        var script = Path.Combine(Path.GetTempPath(), $"swe-script-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, "#!/bin/sh\n" + body + "\n");
        return (workspace, script);
    }

    [Fact]
    public async Task ExternalAgent_EditsWorkspace_ReportsSuccess()
    {
        if (!IsPosix) return;
        // Script writes a file into the workspace passed as {workspace}.
        var (ws, script) = MakeScript("echo patched > \"$1/fix.txt\"");
        try
        {
            var agent = new ExternalCliAgent($"sh {script} {{workspace}}", "test-model", ws, TimeSpan.FromSeconds(30));
            var result = await agent.RunAsync("the problem statement");

            Assert.True(result.Success, result.StopReason);
            Assert.Equal("completed", result.StopReason);
            Assert.True(File.Exists(Path.Combine(ws, "fix.txt")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ExternalAgent_NonZeroExit_ReportsFailureWithStderr()
    {
        if (!IsPosix) return;
        var (ws, script) = MakeScript("echo boom 1>&2; exit 7");
        try
        {
            var agent = new ExternalCliAgent($"sh {script}", "m", ws, TimeSpan.FromSeconds(30));
            var result = await agent.RunAsync("problem");

            Assert.False(result.Success);
            Assert.Contains("exit 7", result.StopReason);
            Assert.Contains("boom", result.StopReason);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ExternalAgent_NoPromptToken_PipesPromptToStdin()
    {
        if (!IsPosix) return;
        // No {prompt}/{prompt_file} token => the problem statement arrives on stdin; the script
        // copies stdin into a known file in the workspace.
        var (ws, script) = MakeScript("cat > \"$1/captured.txt\"");
        try
        {
            var agent = new ExternalCliAgent($"sh {script} {{workspace}}", "m", ws, TimeSpan.FromSeconds(30));
            var result = await agent.RunAsync("STATEMENT-123");

            Assert.True(result.Success, result.StopReason);
            Assert.Equal("STATEMENT-123", File.ReadAllText(Path.Combine(ws, "captured.txt")).Trim());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ExternalAgent_PromptFileToken_WritesStatementToFile()
    {
        if (!IsPosix) return;
        // {prompt_file} => a temp file holding the statement; script copies it into the workspace.
        var (ws, script) = MakeScript("cp \"$2\" \"$1/from_file.txt\"");
        try
        {
            var agent = new ExternalCliAgent($"sh {script} {{workspace}} {{prompt_file}}", "m", ws, TimeSpan.FromSeconds(30));
            var result = await agent.RunAsync("FILE-STATEMENT");

            Assert.True(result.Success, result.StopReason);
            Assert.Equal("FILE-STATEMENT", File.ReadAllText(Path.Combine(ws, "from_file.txt")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ExternalAgent_ExceedsTimeout_ReportsTimeoutAndKills()
    {
        if (!IsPosix) return;
        var (ws, script) = MakeScript("sleep 10");
        try
        {
            var agent = new ExternalCliAgent($"sh {script}", "m", ws, TimeSpan.FromSeconds(1));
            var result = await agent.RunAsync("problem");

            Assert.False(result.Success);
            Assert.Contains("timeout", result.StopReason);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
            File.Delete(script);
        }
    }

    [Fact]
    public async Task ExternalAgent_LaunchFailure_ReportsError()
    {
        var ws = Directory.CreateTempSubdirectory("swe-ws-").FullName;
        try
        {
            var agent = new ExternalCliAgent("this-binary-does-not-exist-xyz {prompt}", "m", ws, TimeSpan.FromSeconds(5));
            var result = await agent.RunAsync("problem");

            Assert.False(result.Success);
            Assert.Contains("failed to launch", result.StopReason);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Selector_Andy_ReturnsAndyFactory()
    {
        var ctx = new RunContext { DatasetPath = "x", Agent = "andy" };
        Assert.IsType<SweAgentFactory>(SweInstanceRunner.SelectAgentFactory(ctx));
    }

    [Fact]
    public void Selector_External_ReturnsExternalFactory()
    {
        var ctx = new RunContext { DatasetPath = "x", Agent = "external", AgentCommand = "opencode run {prompt}" };
        Assert.IsType<ExternalCliAgentFactory>(SweInstanceRunner.SelectAgentFactory(ctx));
    }

    [Fact]
    public void Selector_Unknown_Throws()
    {
        var ctx = new RunContext { DatasetPath = "x", Agent = "nope" };
        Assert.Throws<ArgumentException>(() => SweInstanceRunner.SelectAgentFactory(ctx));
    }

    [Fact]
    public void ExternalFactory_WithoutCommand_ThrowsOnCreate()
    {
        var ctx = new RunContext { DatasetPath = "x", Agent = "external", AgentCommand = null };
        var factory = new ExternalCliAgentFactory(ctx);
        Assert.Throws<InvalidOperationException>(() => factory.Create("/tmp/ws"));
    }

    [Fact]
    public void Cli_ParsesAgentFlags()
    {
        var ctx = SweBenchCliOptions.Parse(
            new[] { "--dataset", "d.jsonl", "--agent", "external", "--agent-cmd", "opencode run --model {model} {prompt}" },
            "run-x");
        Assert.Equal("external", ctx.Agent);
        Assert.Equal("opencode run --model {model} {prompt}", ctx.AgentCommand);
    }

    [Fact]
    public void Cli_DefaultsToAndyAgent()
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "d.jsonl" }, "run-x");
        Assert.Equal("andy", ctx.Agent);
        Assert.Null(ctx.AgentCommand);
    }
}
