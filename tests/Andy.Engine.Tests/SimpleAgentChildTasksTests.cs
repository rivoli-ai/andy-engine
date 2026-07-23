using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Bounded child-agent execution (issue #34): deterministic ordering, ceilings that a child can
/// never widen (workspace/tools/provider/budgets), race-safe aggregate budgets, a cancellation
/// tree that leaves no child running, and isolated per-child histories.
/// </summary>
public class SimpleAgentChildTasksTests : IDisposable
{
    private readonly string _parentDir;

    public SimpleAgentChildTasksTests()
    {
        _parentDir = Path.Combine(Path.GetTempPath(), "child-agents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_parentDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_parentDir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    // ---- helpers -------------------------------------------------------------------------

    private static Mock<IToolRegistry> RegistryWith(params string[] toolIds)
    {
        var registrations = toolIds.Select(id => new ToolRegistration
        {
            IsEnabled = true,
            Metadata = new ToolMetadata { Id = id, Name = id, Description = $"tool {id}" },
        }).ToList();

        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(registrations);
        registry.Setup(r => r.GetTool(It.IsAny<string>()))
            .Returns((string id) => registrations.FirstOrDefault(t => t.Metadata.Id == id));
        return registry;
    }

    private static IToolExecutor SucceedingExecutor(List<ToolExecutionContext>? captureContexts = null)
    {
        var executor = new Mock<IToolExecutor>();
        var setup = executor.Setup(e => e.ExecuteAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()));
        if (captureContexts != null)
        {
            setup.Callback<string, Dictionary<string, object?>, ToolExecutionContext>(
                (_, _, ctx) => { lock (captureContexts) captureContexts.Add(ctx); });
        }
        setup.ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });
        return executor.Object;
    }

    /// <summary>Provider that immediately returns a final text answer (optionally gated / capturing).</summary>
    private static Mock<ILlmProvider> FinalAnswerProvider(
        string answer,
        Task? gate = null,
        List<LlmRequest>? capture = null)
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (LlmRequest req, CancellationToken _) =>
            {
                if (capture != null)
                    lock (capture) capture.Add(req);
                if (gate != null)
                    await gate;
                return new LlmResponse
                {
                    AssistantMessage = new Message { Role = Role.Assistant, Content = answer },
                };
            });
        return provider;
    }

    /// <summary>Provider that emits one tool call per turn forever (never finishes on its own).</summary>
    private static Mock<ILlmProvider> NeverFinishingProvider(Action? onCall = null)
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken _) =>
            {
                onCall?.Invoke();
                return Task.FromResult(new LlmResponse
                {
                    AssistantMessage = new Message
                    {
                        Role = Role.Assistant,
                        Content = "",
                        ToolCalls = new List<ToolCall>
                        {
                            new() { Id = "call_1", Name = "alpha", ArgumentsJson = "{}" },
                        },
                    },
                });
            });
        return provider;
    }

    /// <summary>Provider that blocks until cancelled, signalling when a call has entered.</summary>
    private static Mock<ILlmProvider> BlockingProvider(TaskCompletionSource? entered = null)
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (LlmRequest _, CancellationToken ct) =>
            {
                entered?.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            });
        return provider;
    }

    private SimpleAgent NewParent(
        ILlmProvider provider,
        IToolRegistry? registry = null,
        IToolExecutor? executor = null,
        int maxTurns = 10)
    {
        return new SimpleAgent(
            provider,
            registry ?? RegistryWith("alpha").Object,
            executor ?? SucceedingExecutor(),
            systemPrompt: "parent system",
            maxTurns: maxTurns,
            workingDirectory: _parentDir);
    }

    // ---- deterministic ordering ----------------------------------------------------------

    [Fact]
    public async Task ParallelChildren_ResultsStayInTaskOrder_EvenWhenSecondFinishesFirst()
    {
        // Child 0 is gated on child 1's completion, so child 1 always finishes first.
        var child1Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider0 = FinalAnswerProvider("first result", gate: child1Done.Task);
        var provider1 = FinalAnswerProvider("second result");
        provider1.Setup(p => p.Name).Returns("p1");

        var parent = NewParent(FinalAnswerProvider("unused").Object);
        var report = await RunAndRelease(parent, child1Done, provider0.Object, provider1.Object);

        Assert.Equal(ChildBatchOutcome.Succeeded, report.Outcome);
        Assert.Equal(2, report.Results.Count);
        Assert.Equal(0, report.Results[0].TaskIndex);
        Assert.Equal("slow", report.Results[0].Name);
        Assert.Equal("first result", report.Results[0].Response);
        Assert.Equal(1, report.Results[1].TaskIndex);
        Assert.Equal("fast", report.Results[1].Name);
        Assert.Equal("second result", report.Results[1].Response);
        Assert.All(report.Results, r => Assert.Equal(ChildTaskStatus.Succeeded, r.Status));
    }

    private static async Task<ChildTaskRunReport> RunAndRelease(
        SimpleAgent parent,
        TaskCompletionSource child1Done,
        ILlmProvider provider0,
        ILlmProvider provider1)
    {
        var run = parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask { Name = "slow", Objective = "task zero", ProviderName = "p0" },
                new ChildTask { Name = "fast", Objective = "task one", ProviderName = "p1" },
            },
            new ChildRunOptions
            {
                MaxConcurrency = 2,
                ChildProviders = new Dictionary<string, ILlmProvider> { ["p0"] = provider0, ["p1"] = provider1 },
            },
            onEvent: e =>
            {
                // Child 1 completing releases child 0's gate.
                if (e.Kind == ChildAgentEventKind.Completed && e.ChildName == "fast")
                    child1Done.TrySetResult();
            });
        return await run;
    }

    // ---- ceilings: validation is all-or-nothing ------------------------------------------

    [Fact]
    public async Task UnknownTool_RejectsBatchBeforeAnythingRuns()
    {
        var provider = FinalAnswerProvider("done");
        var parent = NewParent(provider.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "fine task" },
            new ChildTask { Objective = "widening task", AllowedTools = new[] { "not_a_tool" } },
        }));

        provider.Verify(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("sub/../../outside")]
    public async Task WorkspaceEscape_RejectsBatch(string workspace)
    {
        var provider = FinalAnswerProvider("done");
        var parent = NewParent(provider.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "escape attempt", Workspace = workspace },
        }));

        provider.Verify(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AbsoluteWorkspace_RejectsBatch()
    {
        var parent = NewParent(FinalAnswerProvider("done").Object);
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "escape attempt", Workspace = Path.GetTempPath() },
        }));
    }

    [Fact]
    public async Task UnknownProviderName_RejectsBatch()
    {
        var parent = NewParent(FinalAnswerProvider("done").Object);
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "task", ProviderName = "not_offered" },
        }));
    }

    [Fact]
    public async Task ChildTurnBudgetAboveParentCeiling_RejectsBatch()
    {
        var parent = NewParent(FinalAnswerProvider("done").Object, maxTurns: 5);
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "task", MaxTurns = 6 },
        }));
    }

    [Fact]
    public async Task DuplicateChildNames_RejectsBatch()
    {
        var parent = NewParent(FinalAnswerProvider("done").Object);
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Name = "twin", Objective = "task a" },
            new ChildTask { Name = "twin", Objective = "task b" },
        }));
    }

    // ---- tool ceiling at runtime ---------------------------------------------------------

    [Fact]
    public async Task DisallowedTool_IsHiddenFromRequests_AndDeniedIfCalledAnyway()
    {
        var registry = RegistryWith("alpha", "beta");
        var innerExecutorCalls = new List<string>();
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Callback<string, Dictionary<string, object?>, ToolExecutionContext>(
                (id, _, _) => { lock (innerExecutorCalls) innerExecutorCalls.Add(id); })
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });

        // The model hallucinates the disallowed tool "beta" on turn 1, then finishes.
        var requests = new List<LlmRequest>();
        var turn = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest req, CancellationToken _) =>
            {
                lock (requests) requests.Add(req);
                var response = Interlocked.Increment(ref turn) == 1
                    ? new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = new List<ToolCall>
                            {
                                new() { Id = "call_1", Name = "beta", ArgumentsJson = "{}" },
                            },
                        },
                    }
                    : new LlmResponse
                    {
                        AssistantMessage = new Message { Role = Role.Assistant, Content = "finished" },
                    };
                return Task.FromResult(response);
            });

        var parent = NewParent(provider.Object, registry.Object, executor.Object);
        var report = await parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Name = "restricted", Objective = "do it", AllowedTools = new[] { "alpha" } },
        });

        // The disallowed tool never reached the real executor (permission denial is a normal
        // failed tool result the child can recover from), and was never declared to the LLM.
        Assert.Empty(innerExecutorCalls);
        Assert.All(requests, r => Assert.DoesNotContain(r.Tools, t => t.Name == "beta"));
        Assert.Contains(requests[0].Tools, t => t.Name == "alpha");
        Assert.Equal(ChildTaskStatus.Succeeded, report.Results[0].Status);
        Assert.Equal("finished", report.Results[0].Response);
    }

    // ---- aggregate turn budget under concurrency -----------------------------------------

    [Fact]
    public async Task AggregateTurnBudget_HoldsExactly_UnderConcurrentChildren()
    {
        var llmCalls = 0;
        var provider0 = NeverFinishingProvider(() => Interlocked.Increment(ref llmCalls));
        var provider1 = NeverFinishingProvider(() => Interlocked.Increment(ref llmCalls));

        var parent = NewParent(FinalAnswerProvider("unused").Object, maxTurns: 100);
        var report = await parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask { Name = "a", Objective = "spin", ProviderName = "p0" },
                new ChildTask { Name = "b", Objective = "spin", ProviderName = "p1" },
            },
            new ChildRunOptions
            {
                MaxConcurrency = 2,
                MaxTotalTurns = 5,
                ChildProviders = new Dictionary<string, ILlmProvider>
                {
                    ["p0"] = provider0.Object,
                    ["p1"] = provider1.Object,
                },
            });

        // Exactly 5 LLM calls were admitted across both children — the Interlocked acquisition
        // cannot over-admit under the concurrency race — and the report counts exactly the
        // dispatched calls (a denied call must not inflate TotalTurns past the ceiling).
        Assert.Equal(5, llmCalls);
        Assert.Equal(5, report.TotalTurns);
        Assert.All(report.Results, r => Assert.Equal(ChildTaskStatus.BudgetExceeded, r.Status));
        Assert.Equal(ChildBatchOutcome.PartialFailure, report.Outcome);
    }

    // ---- cancellation tree ---------------------------------------------------------------

    [Fact]
    public async Task ParentCancellation_CancelsRunningChild_AndNeverStartsTheRest()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredCalls = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (LlmRequest _, CancellationToken ct) =>
            {
                Interlocked.Increment(ref enteredCalls);
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            });

        var parent = NewParent(provider.Object);
        using var cts = new CancellationTokenSource();
        var run = parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask { Name = "running", Objective = "block" },
                new ChildTask { Name = "queued-1", Objective = "never starts" },
                new ChildTask { Name = "queued-2", Objective = "never starts" },
            },
            new ChildRunOptions { MaxConcurrency = 1 },
            cancellationToken: cts.Token);

        await entered.Task;      // the first child is inside its LLM call
        cts.Cancel();
        var report = await run;  // returns only after all children are done — nothing keeps running

        Assert.Equal(3, report.Results.Count);
        Assert.All(report.Results, r => Assert.Equal(ChildTaskStatus.Cancelled, r.Status));
        Assert.Equal(ChildBatchOutcome.Cancelled, report.Outcome);
        // Queued children were never started: only the first child ever reached the provider.
        Assert.Equal(1, Volatile.Read(ref enteredCalls));
    }

    [Fact]
    public async Task PerChildDeadline_ReportsBudgetExceeded_NotCancelled()
    {
        var parent = NewParent(BlockingProvider().Object);

        var report = await parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Name = "slowpoke", Objective = "block", MaxDuration = TimeSpan.FromMilliseconds(50) },
        });

        var result = Assert.Single(report.Results);
        Assert.Equal(ChildTaskStatus.BudgetExceeded, result.Status);
        Assert.Equal("time_budget_exceeded", result.StopReason);
        Assert.Equal(ChildBatchOutcome.PartialFailure, report.Outcome);
    }

    [Fact]
    public async Task SpuriousOperationCanceled_WithNoDeadline_IsFailed_NotBudgetExceeded()
    {
        // An HttpClient-style internal timeout: TaskCanceledException with no token cancelled.
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

        var parent = NewParent(provider.Object);
        var report = await parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Name = "timeouty", Objective = "call the flaky provider" },
        });

        var result = Assert.Single(report.Results);
        Assert.Equal(ChildTaskStatus.Failed, result.Status);
        Assert.Equal("error", result.StopReason);
    }

    // ---- partial failure -----------------------------------------------------------------

    [Fact]
    public async Task OneChildFailing_DoesNotDisturbSiblingResults()
    {
        var good = FinalAnswerProvider("all good");
        var bad = new Mock<ILlmProvider>();
        bad.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider exploded"));

        var parent = NewParent(FinalAnswerProvider("unused").Object);
        var report = await parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask { Name = "ok", Objective = "succeed", ProviderName = "good" },
                new ChildTask { Name = "broken", Objective = "fail", ProviderName = "bad" },
            },
            new ChildRunOptions
            {
                ChildProviders = new Dictionary<string, ILlmProvider>
                {
                    ["good"] = good.Object,
                    ["bad"] = bad.Object,
                },
            });

        Assert.Equal(ChildBatchOutcome.PartialFailure, report.Outcome);
        Assert.Equal(ChildTaskStatus.Succeeded, report.Results[0].Status);
        Assert.Equal("all good", report.Results[0].Response);
        Assert.Equal(ChildTaskStatus.Failed, report.Results[1].Status);
        Assert.Contains("provider exploded", report.Results[1].Response);
    }

    // ---- workspace -----------------------------------------------------------------------

    [Fact]
    public async Task ChildWorkspace_IsCreated_AndUsedForToolExecution()
    {
        var contexts = new List<ToolExecutionContext>();
        var executor = SucceedingExecutor(contexts);

        // One tool call, then finish — so the executor observes the child's working directory.
        var turn = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken _) => Task.FromResult(
                Interlocked.Increment(ref turn) == 1
                    ? new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = new List<ToolCall>
                            {
                                new() { Id = "call_1", Name = "alpha", ArgumentsJson = "{}" },
                            },
                        },
                    }
                    : new LlmResponse
                    {
                        AssistantMessage = new Message { Role = Role.Assistant, Content = "done" },
                    }));

        var parent = NewParent(provider.Object, executor: executor);
        var report = await parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Name = "nested", Objective = "work in subdir", Workspace = Path.Combine("sub", "dir") },
        });

        var expectedDir = Path.Combine(Path.GetFullPath(_parentDir), "sub", "dir");
        Assert.True(Directory.Exists(expectedDir));
        Assert.Equal(ChildTaskStatus.Succeeded, report.Results[0].Status);
        Assert.Equal(expectedDir, Assert.Single(contexts).WorkingDirectory);
    }

    [Fact]
    public async Task WorkspaceCollidingWithExistingFile_FailsOnlyThatChild()
    {
        // A FILE already sits where the second child wants its workspace directory. Validation
        // cannot see this (it only checks containment) — setup fails at run time, and the batch
        // must still report every task, sibling results intact.
        File.WriteAllText(Path.Combine(_parentDir, "notes"), "not a directory");

        var good = FinalAnswerProvider("sibling survived");
        var parent = NewParent(FinalAnswerProvider("unused").Object);
        var report = await parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask { Name = "ok", Objective = "succeed", ProviderName = "good" },
                new ChildTask { Name = "collides", Objective = "never runs", Workspace = "notes", ProviderName = "good" },
            },
            new ChildRunOptions
            {
                ChildProviders = new Dictionary<string, ILlmProvider> { ["good"] = good.Object },
            });

        Assert.Equal(2, report.Results.Count);
        Assert.Equal(ChildTaskStatus.Succeeded, report.Results[0].Status);
        Assert.Equal("sibling survived", report.Results[0].Response);
        Assert.Equal(ChildTaskStatus.Failed, report.Results[1].Status);
        Assert.Equal("error", report.Results[1].StopReason);
        Assert.Equal(ChildBatchOutcome.PartialFailure, report.Outcome);
    }

    [Fact]
    public async Task ThrowingEventConsumer_DoesNotFaultTheBatch()
    {
        var parent = NewParent(FinalAnswerProvider("done").Object);
        var report = await parent.RunChildTasksAsync(
            new[] { new ChildTask { Name = "solo", Objective = "finish" } },
            onEvent: _ => throw new InvalidOperationException("consumer bug"));

        Assert.Equal(ChildTaskStatus.Succeeded, Assert.Single(report.Results).Status);
        Assert.Equal(ChildBatchOutcome.Succeeded, report.Outcome);
    }

    [Fact]
    public async Task TrailingSeparatorParentDirectory_AcceptsWorkspaceResolvingToParentRoot()
    {
        var provider = FinalAnswerProvider("done");
        var parent = new SimpleAgent(
            provider.Object,
            RegistryWith("alpha").Object,
            SucceedingExecutor(),
            systemPrompt: "parent system",
            workingDirectory: _parentDir + Path.DirectorySeparatorChar);

        // "sub/.." resolves to the parent root itself — legal, not an escape.
        var report = await parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Name = "rooted", Objective = "work", Workspace = Path.Combine("sub", "..") },
        });

        Assert.Equal(ChildTaskStatus.Succeeded, Assert.Single(report.Results).Status);
    }

    // ---- history isolation ---------------------------------------------------------------

    [Fact]
    public async Task ChildHistories_AreIsolated_FromEachOtherAndTheParent()
    {
        var parentProvider = FinalAnswerProvider("parent answer");
        var parent = NewParent(parentProvider.Object);
        await parent.ProcessMessageAsync("PARENT-SECRET conversation");

        var requests0 = new List<LlmRequest>();
        var requests1 = new List<LlmRequest>();
        var provider0 = FinalAnswerProvider("r0", capture: requests0);
        var provider1 = FinalAnswerProvider("r1", capture: requests1);

        await parent.RunChildTasksAsync(
            new[]
            {
                new ChildTask { Name = "one", Objective = "OBJECTIVE-ONE", ProviderName = "p0" },
                new ChildTask { Name = "two", Objective = "OBJECTIVE-TWO", ProviderName = "p1" },
            },
            new ChildRunOptions
            {
                ChildProviders = new Dictionary<string, ILlmProvider>
                {
                    ["p0"] = provider0.Object,
                    ["p1"] = provider1.Object,
                },
            });

        // Neither the parent's history nor the sibling's objective leaks into a child's request.
        var child1Text = string.Join("\n", Assert.Single(requests0).Messages.Select(m => m.Content));
        var child2Text = string.Join("\n", Assert.Single(requests1).Messages.Select(m => m.Content));
        Assert.Contains("OBJECTIVE-ONE", child1Text);
        Assert.DoesNotContain("OBJECTIVE-TWO", child1Text);
        Assert.DoesNotContain("PARENT-SECRET", child1Text);
        Assert.Contains("OBJECTIVE-TWO", child2Text);
        Assert.DoesNotContain("OBJECTIVE-ONE", child2Text);
        Assert.DoesNotContain("PARENT-SECRET", child2Text);
    }

    // ---- lifecycle events ----------------------------------------------------------------

    [Fact]
    public async Task LifecycleEvents_AreEmittedInOrder_WithCorrelation()
    {
        var turn = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken _) => Task.FromResult(
                Interlocked.Increment(ref turn) == 1
                    ? new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = new List<ToolCall>
                            {
                                new() { Id = "call_1", Name = "alpha", ArgumentsJson = "{}" },
                            },
                        },
                    }
                    : new LlmResponse
                    {
                        AssistantMessage = new Message { Role = Role.Assistant, Content = "done" },
                    }));

        var events = new List<ChildAgentEvent>();
        var parent = NewParent(provider.Object);
        var report = await parent.RunChildTasksAsync(
            new[] { new ChildTask { Name = "worker", Objective = "one tool then done" } },
            onEvent: events.Add);

        Assert.Equal(
            new[] { ChildAgentEventKind.Started, ChildAgentEventKind.ToolCalled, ChildAgentEventKind.Completed },
            events.Select(e => e.Kind).ToArray());
        Assert.All(events, e =>
        {
            Assert.Equal(report.ParentRunId, e.ParentRunId);
            Assert.Equal("worker", e.ChildName);
            Assert.Equal(0, e.TaskIndex);
        });
        Assert.Equal("alpha", events[1].ToolName);
        Assert.Equal(ChildTaskStatus.Succeeded, events[2].Status);
    }

    // ---- input validation ----------------------------------------------------------------

    [Fact]
    public async Task EmptyBatch_AndBlankObjective_AreRejected()
    {
        var parent = NewParent(FinalAnswerProvider("done").Object);
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(Array.Empty<ChildTask>()));
        await Assert.ThrowsAsync<ArgumentException>(() => parent.RunChildTasksAsync(new[]
        {
            new ChildTask { Objective = "   " },
        }));
    }
}
