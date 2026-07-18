using System.Runtime.InteropServices;
using Andy.Engine.SweBench.Agent;
using Andy.Engine.SweBench.Model;
using Andy.Engine.SweBench.Orchestration;
using Andy.Tools.Core;
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

    private static SweBenchInstance Inst(string repo = "django/django") => new()
    {
        InstanceId = "x__y-1",
        Repo = repo,
        BaseCommit = "0",
        ProblemStatement = "fix it",
    };

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
        Assert.Throws<InvalidOperationException>(() => factory.Create("/tmp/ws", Inst()));
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

    [Fact]
    public void Cli_ParsesPromptFlags()
    {
        var ctx = SweBenchCliOptions.Parse(
            new[]
            {
                "--dataset", "d.jsonl", "--system-prompt-file", "p.md", "--rules-dir", "rules",
                "--skills-dir", "skills",
            }, "run-x");
        Assert.Equal("p.md", ctx.SystemPromptFile);
        Assert.Equal("rules", ctx.RulesDir);
        Assert.Equal("skills", ctx.SkillsDir);
    }

    [Fact]
    public void Cli_NoSkillsDir_IsNull()
    {
        var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "d.jsonl" }, "run-x");
        Assert.Null(ctx.SkillsDir);
    }

    /// <summary>Writes a minimal valid SKILL.md and returns the skills-root dir.</summary>
    private static string MakeSkillsDir(string name = "pdf-forms", string description = "Fill and extract PDF forms.", string body = "# body\n")
    {
        var skillsDir = Directory.CreateTempSubdirectory("swe-skills-").FullName;
        var skill = Path.Combine(skillsDir, name);
        Directory.CreateDirectory(skill);
        File.WriteAllText(
            Path.Combine(skill, "SKILL.md"),
            $"---\nname: {name}\ndescription: {description}\n---\n{body}");
        return skillsDir;
    }

    [Fact]
    public void AndyAgent_WithSkillsDir_RegistersSkillTools()
    {
        // Composition + skill registration, exercised WITHOUT an API key or network: BuildToolStack
        // builds the tool registry but not the LLM provider. CI (no key) runs this and fails if
        // --skills-dir ever stops registering the tools.
        var skillsDir = MakeSkillsDir();
        var ws = Directory.CreateTempSubdirectory("swe-ws-").FullName;
        try
        {
            var ctx = SweBenchCliOptions.Parse(
                new[] { "--dataset", "d.jsonl", "--skills-dir", skillsDir }, "run-x");
            var stack = new SweAgentFactory(ctx).BuildToolStack(ws, Inst());
            try
            {
                Assert.Contains("skill", stack.AvailableTools);
                Assert.Contains("skill_file", stack.AvailableTools);
            }
            finally { stack.Services.Dispose(); }
        }
        finally
        {
            Directory.Delete(skillsDir, recursive: true);
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task AndyAgent_WithSkillsDir_SkillTool_LoadsBodyThroughExecutor()
    {
        // The registered `skill` tool loads a real temporary SKILL.md body via the executor — no
        // provider, no network.
        var skillsDir = MakeSkillsDir(body: "# PDF Forms\nStep 1. Do the thing.\n");
        var ws = Directory.CreateTempSubdirectory("swe-ws-").FullName;
        try
        {
            var ctx = SweBenchCliOptions.Parse(
                new[] { "--dataset", "d.jsonl", "--skills-dir", skillsDir }, "run-x");
            var stack = new SweAgentFactory(ctx).BuildToolStack(ws, Inst());
            try
            {
                var result = await stack.Executor.ExecuteAsync(
                    "skill",
                    new Dictionary<string, object?> { ["name"] = "pdf-forms" },
                    new ToolExecutionContext { WorkingDirectory = ws });

                Assert.True(result.IsSuccessful, result.ErrorMessage);
                Assert.Contains("Step 1. Do the thing.", ContentOf(result));
            }
            finally { stack.Services.Dispose(); }
        }
        finally
        {
            Directory.Delete(skillsDir, recursive: true);
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void AndyAgent_WithoutSkillsDir_HasNoSkillTools()
    {
        var ws = Directory.CreateTempSubdirectory("swe-ws-").FullName;
        try
        {
            var ctx = SweBenchCliOptions.Parse(new[] { "--dataset", "d.jsonl" }, "run-x");
            var stack = new SweAgentFactory(ctx).BuildToolStack(ws, Inst());
            try
            {
                Assert.DoesNotContain("skill", stack.AvailableTools);
                Assert.DoesNotContain("skill_file", stack.AvailableTools);
            }
            finally { stack.Services.Dispose(); }
        }
        finally { Directory.Delete(ws, recursive: true); }
    }

    [Fact]
    public void AndyAgent_MissingSkillsDir_FailsFast()
    {
        // An invalid --skills-dir must fail at factory construction, before any instance work.
        var ctx = SweBenchCliOptions.Parse(
            new[] { "--dataset", "d.jsonl", "--skills-dir", "/no/such/skills/dir" }, "run-x");
        var ex = Assert.Throws<ArgumentException>(() => new SweAgentFactory(ctx));
        Assert.Contains("skills-dir", ex.Message);
    }

    [Fact]
    public void AndyAgent_EmptySkillsDir_FailsFast()
    {
        // A directory with zero usable skills cannot silently run as the with-skills arm.
        var empty = Directory.CreateTempSubdirectory("swe-skills-empty-").FullName;
        try
        {
            var ctx = SweBenchCliOptions.Parse(
                new[] { "--dataset", "d.jsonl", "--skills-dir", empty }, "run-x");
            var ex = Assert.Throws<ArgumentException>(() => new SweAgentFactory(ctx));
            Assert.Contains("no usable skills", ex.Message);
        }
        finally { Directory.Delete(empty, recursive: true); }
    }

    /// <summary>Extracts the text body from a <c>TextSuccess</c> tool result.</summary>
    private static string ContentOf(Andy.Tools.Core.ToolResult result) =>
        result.Data is IDictionary<string, object?> d && d.TryGetValue("content", out var c)
            ? c?.ToString() ?? string.Empty
            : result.Data?.ToString() ?? string.Empty;
}

/// <summary>Validation and composition tests for the andy agent's external prompt sources.</summary>
public class SwePromptConfigTests
{
    private static string TempFile(string content, string ext = ".md")
    {
        var p = Path.Combine(Path.GetTempPath(), $"swe-prompt-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Default_NoSources_UsesBuiltInPrompt()
    {
        var cfg = SwePromptConfig.Load(null, null);
        var prompt = cfg.Build("/ws/repo", "django/django");
        // Built-in prompt mentions the workspace and the no-test-edit rule.
        Assert.Contains("/ws/repo", prompt);
        Assert.Contains("test", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPromptFile_Replaces_AndSubstitutesWorkspaceToken()
    {
        var f = TempFile("Custom instructions. Operate in {workspace} only.");
        try
        {
            var cfg = SwePromptConfig.Load(f, null);
            var prompt = cfg.Build("/ws/abc", "django/django");
            Assert.StartsWith("Custom instructions.", prompt);
            Assert.Contains("/ws/abc", prompt);
            Assert.DoesNotContain("{workspace}", prompt);
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void SystemPromptFile_NoToken_AppendsWorkdirLine()
    {
        var f = TempFile("Just do the thing.");
        try
        {
            var prompt = SwePromptConfig.Load(f, null).Build("/ws/xyz", "django/django");
            Assert.Contains("Just do the thing.", prompt);
            Assert.Contains("/ws/xyz", prompt);
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void RulesDir_AppendsMatchingRepoRules_ByUnderscoreKey()
    {
        var dir = Directory.CreateTempSubdirectory("swe-rules-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "django__django.md"), "Django: tests live in tests/.");
            File.WriteAllText(Path.Combine(dir, "astropy__astropy.md"), "Astropy: use pytest.");
            var prompt = SwePromptConfig.Load(null, dir).Build("/ws", "django/django");
            Assert.Contains("Django: tests live in tests/.", prompt);
            Assert.DoesNotContain("Astropy: use pytest.", prompt);
            Assert.Contains("Repository-specific rules", prompt);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void RulesDir_AppendsMatchingRepoRules_ByShortKey()
    {
        var dir = Directory.CreateTempSubdirectory("swe-rules-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "django.md"), "short-key rule");
            var prompt = SwePromptConfig.Load(null, dir).Build("/ws", "django/django");
            Assert.Contains("short-key rule", prompt);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void RulesDir_NoMatchingRepo_LeavesBasePromptUnchanged()
    {
        var dir = Directory.CreateTempSubdirectory("swe-rules-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "flask__flask.md"), "irrelevant");
            var prompt = SwePromptConfig.Load(null, dir).Build("/ws", "django/django");
            Assert.DoesNotContain("Repository-specific rules", prompt);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static Andy.Skills.Skill MakeSkill(string name, string description) => new()
    {
        Name = name,
        Description = description,
        DirectoryPath = $"/s/{name}",
        ManifestPath = $"/s/{name}/SKILL.md",
        BodyOffset = 0,
    };

    [Fact]
    public void AppendSkillsBlock_ListsSkills_AndPointsAtTool()
    {
        var prompt = SwePromptConfig.AppendSkillsBlock(
            "Base prompt.",
            [MakeSkill("pdf-forms", "Fill and extract PDF forms."), MakeSkill("react-review", "Review React code.")]);

        Assert.StartsWith("Base prompt.", prompt);
        Assert.Contains("call the `skill` tool", prompt);
        Assert.Contains("pdf-forms: Fill and extract PDF forms.", prompt);
        Assert.Contains("react-review: Review React code.", prompt);
    }

    [Fact]
    public void AppendSkillsBlock_NoSkills_ReturnsPromptUnchanged()
    {
        Assert.Equal("Base prompt.", SwePromptConfig.AppendSkillsBlock("Base prompt.", []));
    }

    [Fact]
    public void AppendSkillsBlock_DoesNotLeakManifestOrHostPaths()
    {
        // Lazy disclosure must expose names/descriptions only — never the SKILL.md manifest path or
        // any host filesystem location (those live outside the workspace-scoped file permissions and
        // would only provoke denied read_file calls).
        var prompt = SwePromptConfig.AppendSkillsBlock(
            "Base prompt.",
            [MakeSkill("pdf-forms", "Fill and extract PDF forms."), MakeSkill("react-review", "Review React code.")]);

        Assert.DoesNotContain("SKILL.md", prompt);
        Assert.DoesNotContain("/s/pdf-forms", prompt);       // DirectoryPath / ManifestPath root
        Assert.DoesNotContain("/s/react-review", prompt);
    }

    [Fact]
    public void Validate_MissingPromptFile_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SwePromptConfig.Load("/no/such/file.md", null));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Validate_PromptFileIsDirectory_Throws()
    {
        var dir = Directory.CreateTempSubdirectory("swe-dir-").FullName;
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => SwePromptConfig.Load(dir, null));
            Assert.Contains("directory", ex.Message);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Validate_EmptyPromptFile_Throws()
    {
        var f = TempFile("   \n  ");
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => SwePromptConfig.Load(f, null));
            Assert.Contains("empty", ex.Message);
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Validate_OversizePromptFile_Throws()
    {
        var f = TempFile(new string('x', (int)SwePromptConfig.MaxPromptFileBytes + 1));
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => SwePromptConfig.Load(f, null));
            Assert.Contains("limit", ex.Message);
        }
        finally { File.Delete(f); }
    }

    [Fact]
    public void Validate_BinaryPromptFile_Throws()
    {
        var p = Path.Combine(Path.GetTempPath(), $"swe-bin-{Guid.NewGuid():N}.md");
        File.WriteAllBytes(p, new byte[] { 0x41, 0x00, 0x42 }); // contains NUL
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => SwePromptConfig.Load(p, null));
            Assert.Contains("binary", ex.Message);
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Validate_MissingRulesDir_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SwePromptConfig.Load(null, "/no/such/dir"));
        Assert.Contains("directory", ex.Message);
    }
}

/// <summary>
/// Security + behavior tests for the <c>skill_file</c> tool: a skill may read its OWN package
/// resources, but nothing outside its directory (another skill, the parent root, an arbitrary host
/// path, or a symlink that escapes). No network or Docker.
/// </summary>
public class SkillResourceToolTests
{
    private static bool IsPosix => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Builds a skills root containing skill <paramref name="name"/> with a <c>references/api.md</c>
    /// resource, plus a sibling <c>secret.txt</c> in the root (outside any skill). Returns
    /// (skillsRoot, tool).
    /// </summary>
    private static (string root, SkillResourceTool tool) MakeTool(string name = "pdf-forms")
    {
        var root = Directory.CreateTempSubdirectory("swe-skills-").FullName;
        var skill = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(skill, "references"));
        File.WriteAllText(
            Path.Combine(skill, "SKILL.md"),
            $"---\nname: {name}\ndescription: A skill.\n---\n# body\n");
        File.WriteAllText(Path.Combine(skill, "references", "api.md"), "RESOURCE-CONTENT");
        File.WriteAllText(Path.Combine(root, "secret.txt"), "TOP-SECRET");

        var opts = new Andy.Skills.Tools.SkillCatalogOptions();
        opts.Roots.Add(root);
        var tool = new SkillResourceTool(new Andy.Skills.Tools.SkillCatalog(opts));
        tool.InitializeAsync().GetAwaiter().GetResult(); // ToolBase.ExecuteAsync requires init first
        return (root, tool);
    }

    private static Task<ToolResult> Run(SkillResourceTool tool, string skill, string path, CancellationToken ct = default) =>
        tool.ExecuteAsync(
            new Dictionary<string, object?> { ["skill"] = skill, ["path"] = path },
            new ToolExecutionContext { CancellationToken = ct });

    private static string ContentOf(ToolResult result) =>
        result.Data is IDictionary<string, object?> d && d.TryGetValue("content", out var c)
            ? c?.ToString() ?? string.Empty
            : result.Data?.ToString() ?? string.Empty;

    [Fact]
    public async Task ReadsResourceWithinSkillDirectory()
    {
        var (root, tool) = MakeTool();
        try
        {
            var result = await Run(tool, "pdf-forms", "references/api.md");
            Assert.True(result.IsSuccessful, result.ErrorMessage);
            Assert.Equal("RESOURCE-CONTENT", ContentOf(result));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DeniesParentTraversal()
    {
        var (root, tool) = MakeTool();
        try
        {
            // secret.txt lives in the skills ROOT, one level above the skill directory.
            var result = await Run(tool, "pdf-forms", "../secret.txt");
            Assert.False(result.IsSuccessful);
            Assert.Contains("denied", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DeniesAbsolutePath()
    {
        var (root, tool) = MakeTool();
        try
        {
            var result = await Run(tool, "pdf-forms", Path.Combine(root, "secret.txt"));
            Assert.False(result.IsSuccessful);
            Assert.Contains("relative", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DeniesSymlinkEscape()
    {
        if (!IsPosix) return;
        var (root, tool) = MakeTool();
        try
        {
            var link = Path.Combine(root, "pdf-forms", "escape.txt");
            File.CreateSymbolicLink(link, Path.Combine(root, "secret.txt"));

            var result = await Run(tool, "pdf-forms", "escape.txt");
            Assert.False(result.IsSuccessful);
            Assert.Contains("denied", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MissingResource_Fails()
    {
        var (root, tool) = MakeTool();
        try
        {
            var result = await Run(tool, "pdf-forms", "references/nope.md");
            Assert.False(result.IsSuccessful);
            Assert.Contains("No resource", result.ErrorMessage ?? "");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UnknownSkill_Fails()
    {
        var (root, tool) = MakeTool();
        try
        {
            var result = await Run(tool, "does-not-exist", "references/api.md");
            Assert.False(result.IsSuccessful);
            Assert.Contains("No skill named", result.ErrorMessage ?? "");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Cancellation_ReportsCancelled()
    {
        var (root, tool) = MakeTool();
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            // ToolBase.ExecuteAsync catches the cancellation and reports it as a failed result.
            var result = await Run(tool, "pdf-forms", "references/api.md", cts.Token);
            Assert.False(result.IsSuccessful);
            Assert.Contains("cancel", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
