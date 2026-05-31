using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Hardening regressions for how the agent loop records conversation history in two awkward
/// exit paths:
///   1. Reaching <c>maxTurns</c> while the last assistant message carried tool calls — the
///      message must not be duplicated (once inside the interleaved log, once as the Turn's
///      final answer).
///   2. The output-limit truncation "continue" path — the partial assistant message and the
///      nudge must be persisted in history (not live only in the in-flight buffer), and they
///      must round-trip cleanly when the run later completes.
/// </summary>
public class SimpleAgentHistoryFidelityTests
{
    private static SimpleAgent NewAgent(ILlmProvider provider, IToolExecutor executor, int maxTurns)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(
            provider,
            registry.Object,
            executor,
            systemPrompt: "system",
            maxTurns: maxTurns);
    }

    private static LlmResponse ToolCallResponse(string callId, string toolName) => new LlmResponse
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = "",
            ToolCalls = new List<ToolCall>
            {
                new ToolCall { Id = callId, Name = toolName, ArgumentsJson = "{}" },
            },
        },
    };

    private static Mock<IToolExecutor> AlwaysSucceedingExecutor()
    {
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok", Message = "ok" });
        return executor;
    }

    /// <summary>
    /// REGRESSION (Bug A): with maxTurns reached while the model is still issuing tool calls,
    /// the last assistant(tool_calls) message was being recorded BOTH inside the interleaved
    /// ToolMessages AND as the Turn's final AssistantMessage, so the reconstructed history
    /// contained it twice. Each assistant tool-call message must appear exactly once.
    /// </summary>
    [Fact]
    public async Task MaxTurnsExceeded_DoesNotDuplicate_FinalAssistantMessage()
    {
        // Every turn the model just asks for another tool call, so the run can only end by
        // exhausting maxTurns.
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ToolCallResponse("call_loop", "spin"));

        var agent = NewAgent(provider.Object, AlwaysSucceedingExecutor().Object, maxTurns: 3);

        var result = await agent.ProcessMessageAsync("go");

        Assert.False(result.Success);
        Assert.Equal("max_turns_exceeded", result.StopReason);

        var history = agent.GetHistory();

        // The reconstructed history must contain no duplicated message instances.
        Assert.Equal(history.Count, history.Distinct().Count());

        // Each assistant message that carried tool calls must be immediately usable as a
        // tool-call message and must appear exactly once. With 3 turns we expect exactly 3
        // assistant tool-call messages (one per turn), not 4 (which a duplicate would add).
        var assistantToolCallMessages = history
            .Where(m => m.Role == Role.Assistant && m.ToolCalls is { Count: > 0 })
            .ToList();
        Assert.Equal(3, assistantToolCallMessages.Count);

        // Every tool-result message in the recorded history is backed by a preceding
        // assistant tool-call with the matching id (no orphaned/dangling pairs).
        var callIds = history
            .Where(m => m.ToolCalls is { Count: > 0 })
            .SelectMany(m => m.ToolCalls!)
            .Select(tc => tc.Id)
            .ToHashSet();
        var toolResults = history
            .Where(m => m.ToolResults is { Count: > 0 })
            .SelectMany(m => m.ToolResults!)
            .ToList();
        Assert.NotEmpty(toolResults);
        Assert.All(toolResults, tr => Assert.Contains(tr.CallId, callIds));
    }

    /// <summary>
    /// REGRESSION (Bug B): when a response is truncated by the output limit, the agent appends a
    /// partial assistant message and a "continue" nudge. Those were only added to the in-flight
    /// buffer, so they were dropped from the persisted conversation history. They must survive in
    /// the recorded Turn once the run completes.
    /// </summary>
    [Fact]
    public async Task OutputLimitTruncation_PersistsPartialAssistantAndNudge_InHistory()
    {
        const string partialContent = "Here is the start of my answer that got cut off";

        // Turn 1: a truncated (FinishReason=length) plain message, no tool calls.
        // Turn 2: a genuine final answer.
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = partialContent },
                FinishReason = "length",
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Final complete answer." },
                FinishReason = "stop",
            });

        var agent = NewAgent(provider.Object, AlwaysSucceedingExecutor().Object, maxTurns: 10);

        var result = await agent.ProcessMessageAsync("do the task");

        Assert.True(result.Success);
        Assert.Equal("Final complete answer.", result.Response);

        var history = agent.GetHistory();

        // The partial assistant message is preserved.
        Assert.Contains(history, m => m.Role == Role.Assistant && m.Content == partialContent);

        // The continue nudge (a synthetic user message) is preserved.
        Assert.Contains(history, m =>
            m.Role == Role.User &&
            m.Content != null &&
            m.Content.Contains("cut off by the output limit"));

        // The genuine final answer is the last message and is recorded once.
        Assert.Equal("Final complete answer.", history[^1].Content);
        Assert.Equal(1, history.Count(m => m.Content == "Final complete answer."));

        // No duplicated message instances.
        Assert.Equal(history.Count, history.Distinct().Count());
    }

    /// <summary>
    /// REGRESSION (Bug A + B combined): a run that truncates and THEN exhausts maxTurns must
    /// still record every partial message exactly once with no duplicated final message.
    /// </summary>
    [Fact]
    public async Task TruncationThenMaxTurns_RecordsEachMessageOnce()
    {
        // The model is perpetually truncated and never produces a tool call or final answer.
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "partial" },
                FinishReason = "max_tokens",
            });

        var agent = NewAgent(provider.Object, AlwaysSucceedingExecutor().Object, maxTurns: 2);

        var result = await agent.ProcessMessageAsync("go");

        Assert.False(result.Success);
        Assert.Equal("max_turns_exceeded", result.StopReason);

        var history = agent.GetHistory();

        // No duplicated instances even though the loop ended on a truncated (non-final) message.
        Assert.Equal(history.Count, history.Distinct().Count());

        // Two truncated assistant messages (one per turn), each recorded once.
        Assert.Equal(2, history.Count(m => m.Role == Role.Assistant && m.Content == "partial"));
    }
}
