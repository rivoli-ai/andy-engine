using System.Diagnostics;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// End-to-end proof for issue #68 that the git worktree tools (Andy.Tools 2026.9.8-rc.98+)
/// compose with bounded child agents (issue #63): the real git_worktree_add tool creates a
/// sibling worktree through the real registry and executor, and a child agent whose Workspace
/// is that absolute path - opted in via ChildRunOptions.AdditionalWorkspaceRoots - executes a
/// real git tool inside it. The LLM is scripted; everything below it is production wiring.
/// </summary>
public class ChildAgentWorktreeIntegrationTests : IDisposable
{
    /// <summary>
    /// Host-style capability grant. SimpleAgent builds its ToolExecutionContext without
    /// Permissions, and ToolPermissions defaults ProcessExecution to false, so every
    /// process-executing tool (all git tools) is refused through a raw executor. Real hosts
    /// solve this with an executor decorator that grants capabilities and enforces consent in
    /// their own permission layer (see andy-cli's UiUpdatingToolExecutor.GrantGatedCapabilities);
    /// this decorator is the minimal test-side equivalent.
    /// </summary>
    private sealed class CapabilityGrantingExecutor : IToolExecutor
    {
        private readonly IToolExecutor _inner;

        public CapabilityGrantingExecutor(IToolExecutor inner) => _inner = inner;

        private static void Grant(ToolExecutionContext context)
        {
            context.Permissions.FileSystemAccess = true;
            context.Permissions.NetworkAccess = true;
            context.Permissions.ProcessExecution = true;
            context.Permissions.EnvironmentAccess = true;
        }

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request)
        {
            Grant(request.Context);
            return _inner.ExecuteAsync(request);
        }

        public Task<ToolExecutionResult> ExecuteAsync(string toolId, Dictionary<string, object?> parameters, ToolExecutionContext? context = null)
        {
            context ??= new ToolExecutionContext();
            Grant(context);
            return _inner.ExecuteAsync(toolId, parameters, context);
        }

        public Task<IList<string>> ValidateExecutionRequestAsync(ToolExecutionRequest request) => _inner.ValidateExecutionRequestAsync(request);
        public Task<ToolResourceUsage?> EstimateResourceUsageAsync(string toolId, Dictionary<string, object?> parameters) => _inner.EstimateResourceUsageAsync(toolId, parameters);
        public Task<int> CancelExecutionsAsync(string correlationId) => _inner.CancelExecutionsAsync(correlationId);
        public IReadOnlyList<RunningExecutionInfo> GetRunningExecutions() => _inner.GetRunningExecutions();
        public ToolExecutionStatistics GetStatistics() => _inner.GetStatistics();

        public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted
        {
            add => _inner.ExecutionStarted += value;
            remove => _inner.ExecutionStarted -= value;
        }

        public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted
        {
            add => _inner.ExecutionCompleted += value;
            remove => _inner.ExecutionCompleted -= value;
        }

        public event EventHandler<SecurityViolationEventArgs>? SecurityViolation
        {
            add => _inner.SecurityViolation += value;
            remove => _inner.SecurityViolation -= value;
        }
    }

    private readonly string _repoDir;
    private readonly string _lanesDir;
    private readonly bool _gitAvailable;
    private readonly ServiceProvider _serviceProvider;
    private readonly IToolRegistry _registry;
    private readonly IToolExecutor _executor;

    public ChildAgentWorktreeIntegrationTests()
    {
        _gitAvailable = IsGitAvailable();
        var suffix = Guid.NewGuid().ToString("N");
        _repoDir = Path.Combine(Path.GetTempPath(), "andy-wt-child-repo-" + suffix);
        _lanesDir = Path.Combine(Path.GetTempPath(), "andy-wt-child-lanes-" + suffix);
        Directory.CreateDirectory(_repoDir);
        Directory.CreateDirectory(_lanesDir);

        if (_gitAvailable)
        {
            RunGit("init -q");
            RunGit("config user.name \"Test User\"");
            RunGit("config user.email \"test@example.com\"");
            RunGit("config commit.gpgsign false");
            File.WriteAllText(Path.Combine(_repoDir, "file.txt"), "hello\n");
            RunGit("add file.txt");
            RunGit("commit -q -m \"Initial commit\"");
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools(options => options.RegisterBuiltInTools = true);
        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.GetRequiredService<IToolLifecycleManager>().InitializeAsync().GetAwaiter().GetResult();
        _registry = _serviceProvider.GetRequiredService<IToolRegistry>();
        _executor = new CapabilityGrantingExecutor(_serviceProvider.GetRequiredService<IToolExecutor>());
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        foreach (var dir in new[] { _lanesDir, _repoDir })
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }

        GC.SuppressFinalize(this);
    }

    private static bool IsGitAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process!.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void RunGit(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repoDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        var stderr = process!.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments} failed: {stderr}");
        }
    }

    /// <summary>Creates a worktree through the real tool and executor, as the parent agent would.</summary>
    private async Task<string> CreateWorktreeAsync(string name, string branch)
    {
        var path = Path.Combine(_lanesDir, name);
        var result = await _executor.ExecuteAsync(
            "git_worktree_add",
            new Dictionary<string, object?> { ["path"] = path, ["branch"] = branch },
            new ToolExecutionContext
            {
                WorkingDirectory = _repoDir,
                Permissions = new ToolPermissions { ProcessExecution = true },
            });

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.True(Directory.Exists(path));
        return path;
    }

    [Fact]
    public async Task WorktreeAddTool_ThroughRealExecutor_CreatesSiblingLane()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var path = await CreateWorktreeAsync("lane-tool", "feature/lane-tool");

        Assert.True(File.Exists(Path.Combine(path, "file.txt")));
        Assert.True(File.Exists(Path.Combine(path, ".git")));
    }

    [Fact]
    public async Task ChildAgent_ExecutesRealGitToolInsideWorktree()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var worktree = await CreateWorktreeAsync("lane-child", "feature/lane-child");

        // A file only the worktree has, so the observed status is provably from the worktree
        // and not from the parent repository checkout.
        File.WriteAllText(Path.Combine(worktree, "only-in-worktree.txt"), "x\n");

        // Scripted provider: first turn calls git_status, second turn reads the tool result and
        // finishes. The requests are captured so the test can inspect what the tool reported.
        var requests = new List<LlmRequest>();
        var turn = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest req, CancellationToken _) =>
            {
                lock (requests)
                {
                    requests.Add(req);
                }

                return Task.FromResult(Interlocked.Increment(ref turn) == 1
                    ? new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = new List<ToolCall>
                            {
                                new() { Id = "call_1", Name = "git_status", ArgumentsJson = "{}" },
                            },
                        },
                    }
                    : new LlmResponse
                    {
                        AssistantMessage = new Message { Role = Role.Assistant, Content = "done" },
                    });
            });

        var parent = new SimpleAgent(
            provider.Object,
            _registry,
            _executor,
            systemPrompt: "parent system",
            maxTurns: 5,
            workingDirectory: _repoDir);

        var report = await parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask
                {
                    Name = "laned",
                    Objective = "report the status of your workspace",
                    Workspace = worktree,
                    AllowedTools = new[] { "git_status" },
                },
            },
            new ChildRunOptions { AdditionalWorkspaceRoots = new[] { _lanesDir } });

        Assert.Equal(ChildTaskStatus.Succeeded, Assert.Single(report.Results).Status);
        Assert.Equal("done", report.Results[0].Response);

        // The second request carries the tool result. It must reflect the WORKTREE: its branch
        // and its unique untracked file, neither of which exists in the parent checkout.
        Assert.Equal(2, requests.Count);
        var toolRoundText = string.Join("\n", requests[1].Messages.Select(m => m.Content));
        Assert.Contains("feature/lane-child", toolRoundText);
        Assert.Contains("only-in-worktree.txt", toolRoundText);
    }

    [Fact]
    public async Task ChildAgent_WorktreeWithoutOptIn_IsRejectedUpFront()
    {
        if (!_gitAvailable)
        {
            return;
        }

        var worktree = await CreateWorktreeAsync("lane-denied", "feature/lane-denied");

        var parent = new SimpleAgent(
            new Mock<ILlmProvider>().Object,
            _registry,
            _executor,
            systemPrompt: "parent system",
            maxTurns: 5,
            workingDirectory: _repoDir);

        // Same worktree, but the parent did not declare AdditionalWorkspaceRoots: the batch is
        // rejected before any child starts, preserving the workspace ceiling.
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "escape", Workspace = worktree },
        }));
    }
}
