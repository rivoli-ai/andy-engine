using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Bounded continuation across per-window turn limits (issue #50).
/// </summary>
public class SimpleAgentContinuationTests
{
    private static Mock<IToolRegistry> CreateRegistry()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>
        {
            new()
            {
                IsEnabled = true,
                Metadata = new ToolMetadata
                {
                    Id = "work",
                    Name = "Work",
                    Description = "Performs one unit of work.",
                },
            },
        });
        return registry;
    }

    private static LlmResponse ToolCallResponse(int index, bool equivalent = false) => new()
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = $"Working on unit {index}.",
            ToolCalls = new List<ToolCall>
            {
                new()
                {
                    Id = $"call_{index}",
                    Name = "work",
                    ArgumentsJson = equivalent ? "{\"unit\":\"same\"}" : $"{{\"unit\":{index}}}",
                },
            },
        },
        FinishReason = "tool_calls",
    };

    private static LlmResponse FinalResponse(string content = "Done.") => new()
    {
        AssistantMessage = new Message { Role = Role.Assistant, Content = content },
        FinishReason = "stop",
    };

    private static SimpleAgent CreateAgent(
        ILlmProvider provider,
        IToolExecutor executor,
        int maxTurns,
        AgentContinuationPolicy? continuationPolicy = null) =>
        new(
            provider,
            CreateRegistry().Object,
            executor,
            "system",
            maxTurns: maxTurns,
            workingDirectory: "/tmp/repository",
            continuationPolicy: continuationPolicy);

    private static Mock<IToolExecutor> CreateExecutor(Func<int, string>? resultFactory = null)
    {
        var invocation = 0;
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                "work",
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(() =>
            {
                var number = Interlocked.Increment(ref invocation);
                return new ToolExecutionResult
                {
                    IsSuccessful = true,
                    Data = resultFactory?.Invoke(number) ?? $"completed unit {number}",
                };
            });
        return executor;
    }

    [Fact]
    public async Task CompletesAcrossWindows_WithoutReplayingToolSideEffects()
    {
        var requests = new List<LlmRequest>();
        var providerCall = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() =>
            {
                var current = Interlocked.Increment(ref providerCall);
                return current <= 3 ? ToolCallResponse(current) : FinalResponse();
            });
        var executor = CreateExecutor();
        var events = new List<AgentContinuationEventArgs>();
        var agent = CreateAgent(
            provider.Object,
            executor.Object,
            maxTurns: 2,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 6,
                MaxContinuationWindows = 2,
            });
        agent.ContinuationProgress += (_, e) => events.Add(e);

        var result = await agent.ProcessMessageAsync("Complete all three work units.");

        result.Success.Should().BeTrue();
        result.TurnCount.Should().Be(4);
        result.Response.Should().Be("Done.");
        executor.Verify(e => e.ExecuteAsync(
            "work",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<ToolExecutionContext>()), Times.Exactly(3));

        var continuedRequest = requests[2];
        continuedRequest.Messages.Should().Contain(
            m => m.Role == Role.User &&
                 (m.Content ?? string.Empty).Contains("[Continuation checkpoint]", StringComparison.Ordinal));
        var checkpoint = continuedRequest.Messages
            .First(m => (m.Content ?? string.Empty).Contains("[Continuation checkpoint]", StringComparison.Ordinal))
            .Content!;
        checkpoint.Should().Contain("Objective:");
        checkpoint.Should().Contain("Complete all three work units.");
        checkpoint.Should().Contain("Completed work:");
        checkpoint.Should().Contain("Observed tool outcomes:");
        checkpoint.Should().Contain("Current repository/task state:");
        checkpoint.Should().Contain("Remaining work:");
        AssertValidToolCorrelation(continuedRequest.Messages);

        events.Select(e => e.Kind).Should().Equal(
            AgentContinuationEventKind.WindowStarted,
            AgentContinuationEventKind.WindowCompleted,
            AgentContinuationEventKind.CheckpointCreated,
            AgentContinuationEventKind.WindowStarted,
            AgentContinuationEventKind.Completed);
        events.Select(e => e.RunId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task TotalTurnCeiling_IsStrict()
    {
        var call = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ToolCallResponse(Interlocked.Increment(ref call)));
        var executor = CreateExecutor();
        var agent = CreateAgent(
            provider.Object,
            executor.Object,
            maxTurns: 2,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 3,
                MaxContinuationWindows = 5,
            });

        var result = await agent.ProcessMessageAsync("Keep working.");

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be("continuation_total_turns_exceeded");
        result.TurnCount.Should().Be(3);
        provider.Verify(p => p.CompleteAsync(
            It.IsAny<LlmRequest>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ContinuationWindowCeiling_IsStrict()
    {
        var call = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ToolCallResponse(Interlocked.Increment(ref call)));
        var agent = CreateAgent(
            provider.Object,
            CreateExecutor().Object,
            maxTurns: 1,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 10,
                MaxContinuationWindows = 1,
            });

        var result = await agent.ProcessMessageAsync("Keep working.");

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be("continuation_windows_exceeded");
        result.TurnCount.Should().Be(2);
    }

    [Fact]
    public async Task ElapsedTimeCeiling_CancelsInFlightWork_AndReturnsDistinctReason()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<LlmRequest, CancellationToken>(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return FinalResponse("unreachable");
            });
        var agent = CreateAgent(
            provider.Object,
            CreateExecutor().Object,
            maxTurns: 2,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 6,
                MaxContinuationWindows = 2,
                MaxElapsedTime = TimeSpan.FromMilliseconds(100),
            });

        var result = await agent.ProcessMessageAsync("Work until the deadline.");

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be("continuation_time_exceeded");
        result.TurnCount.Should().Be(1);
        agent.Conversation.Turns.Should().ContainSingle();
    }

    [Fact]
    public async Task EquivalentCheckpoints_StopNoProgressLoop()
    {
        var call = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ToolCallResponse(
                Interlocked.Increment(ref call),
                equivalent: true));
        var executor = CreateExecutor(_ => "unchanged");
        var events = new List<AgentContinuationEventArgs>();
        var agent = CreateAgent(
            provider.Object,
            executor.Object,
            maxTurns: 1,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 10,
                MaxContinuationWindows = 5,
                EquivalentCheckpointLimit = 1,
            });
        agent.ContinuationProgress += (_, e) => events.Add(e);

        var result = await agent.ProcessMessageAsync("Make progress.");

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be("continuation_no_progress");
        result.TurnCount.Should().Be(2);
        events.Should().Contain(e => e.Kind == AgentContinuationEventKind.NoProgressDetected);
        executor.Verify(e => e.ExecuteAsync(
            "work",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<ToolExecutionContext>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReturningToEarlierCheckpoint_DetectsOscillation()
    {
        var call = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var current = Interlocked.Increment(ref call);
                return ToolCallResponse(current, equivalent: current is 1 or 3);
            });
        var agent = CreateAgent(
            provider.Object,
            CreateExecutor(_ => "unchanged").Object,
            maxTurns: 1,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 10,
                MaxContinuationWindows = 5,
                EquivalentCheckpointLimit = 1,
            });

        var result = await agent.ProcessMessageAsync("Avoid cycling between approaches.");

        result.StopReason.Should().Be("continuation_no_progress");
        result.TurnCount.Should().Be(3);
    }

    [Fact]
    public async Task CancellationDuringCheckpoint_CommitsConsistentTranscript_AndPropagates()
    {
        using var cts = new CancellationTokenSource();
        var checkpointStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCallResponse(1));
        var agent = CreateAgent(
            provider.Object,
            CreateExecutor().Object,
            maxTurns: 1,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 4,
                MaxContinuationWindows = 2,
                CheckpointFactory = async (_, token) =>
                {
                    checkpointStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return "unreachable";
                },
            });

        var run = agent.ProcessMessageAsync("Perform one unit then continue.", cts.Token);
        await checkpointStarted.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        var turn = agent.Conversation.Turns.Should().ContainSingle().Subject;
        turn.UserOrSystemMessage!.Content.Should().Be("Perform one unit then continue.");
        turn.AssistantMessage.Should().BeNull();
        turn.ToolMessages.Should().HaveCount(2);
        AssertValidToolCorrelation(turn.ToolMessages!);
    }

    [Fact]
    public async Task CancellationDuringContinuedWindow_CommitsPriorSideEffects_AndPropagates()
    {
        using var cts = new CancellationTokenSource();
        var continuedWindowStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var providerCall = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<LlmRequest, CancellationToken>(async (_, token) =>
            {
                if (Interlocked.Increment(ref providerCall) == 1)
                    return ToolCallResponse(1);

                continuedWindowStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return FinalResponse("unreachable");
            });
        var executor = CreateExecutor();
        var agent = CreateAgent(
            provider.Object,
            executor.Object,
            maxTurns: 1,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 4,
                MaxContinuationWindows = 2,
            });

        var run = agent.ProcessMessageAsync("Perform work and continue.", cts.Token);
        await continuedWindowStarted.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        var turn = agent.Conversation.Turns.Should().ContainSingle().Subject;
        turn.ToolMessages.Should().HaveCount(2);
        AssertValidToolCorrelation(turn.ToolMessages!);
        executor.Verify(e => e.ExecuteAsync(
            "work",
            It.IsAny<Dictionary<string, object?>>(),
            It.IsAny<ToolExecutionContext>()), Times.Once);
    }

    [Fact]
    public async Task ContinuedRunTranscript_RoundTripsWithEveryExecutedOutcome()
    {
        var providerCall = 0;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var current = Interlocked.Increment(ref providerCall);
                return current <= 2 ? ToolCallResponse(current) : FinalResponse();
            });
        var agent = CreateAgent(
            provider.Object,
            CreateExecutor().Object,
            maxTurns: 1,
            new AgentContinuationPolicy
            {
                MaxTotalTurns = 5,
                MaxContinuationWindows = 3,
                EquivalentCheckpointLimit = 2,
            });

        var result = await agent.ProcessMessageAsync("Complete two units.");
        var snapshot = agent.ExportTranscript();

        result.Success.Should().BeTrue();
        var transcriptTurn = snapshot.Turns.Should().ContainSingle().Subject;
        transcriptTurn.Interleaved.Should().HaveCount(4);
        transcriptTurn.Interleaved.SelectMany(m => m.ToolResults)
            .Select(r => r.CallId)
            .Should()
            .Equal("call_1", "call_2");

        var restored = CreateAgent(
            Mock.Of<ILlmProvider>(),
            Mock.Of<IToolExecutor>(),
            maxTurns: 1);
        restored.RestoreTranscript(TranscriptSnapshot.FromJson(snapshot.ToJson()));
        restored.ExportTranscript().ToJson().Should().Be(snapshot.ToJson());
    }

    [Fact]
    public async Task MissingContinuationPolicy_PreservesExistingMaxTurnsBehavior()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCallResponse(1, equivalent: true));
        var events = new List<AgentContinuationEventArgs>();
        var agent = CreateAgent(
            provider.Object,
            CreateExecutor().Object,
            maxTurns: 2);
        agent.ContinuationProgress += (_, e) => events.Add(e);

        var result = await agent.ProcessMessageAsync("Keep working.");

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be("max_turns_exceeded");
        result.TurnCount.Should().Be(2);
        events.Should().BeEmpty();
    }

    private static void AssertValidToolCorrelation(IReadOnlyList<Message> messages)
    {
        var callIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            foreach (var call in message.ToolCalls ?? [])
                callIds.Add(call.Id);
            foreach (var result in message.ToolResults ?? [])
                callIds.Should().Contain(result.CallId);
        }
    }
}
