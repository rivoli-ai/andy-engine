using System.Collections.Concurrent;
using System.Text.Json;
using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// When the model returns several tool calls in one turn, the agent runs them concurrently to
/// cut latency. Correctness must be preserved: tool-result messages must come back in the SAME
/// order as the tool calls (the model maps results to calls by order/CallId), and a failure in
/// one call must be isolated to that call's error result without failing the siblings.
/// </summary>
public class SimpleAgentParallelToolsTests
{
    private static SimpleAgent NewAgent(ILlmProvider provider, IToolExecutor executor)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(
            provider,
            registry.Object,
            executor,
            systemPrompt: "system",
            maxTurns: 10);
    }

    /// <summary>
    /// Turn 1: assistant emits THREE tool calls. Turn 2: a plain final answer. The fake executor
    /// completes the calls OUT OF ORDER (varying delays), so a serial implementation and a correct
    /// parallel one are distinguishable: the result order must still match the call order.
    /// </summary>
    private static Mock<ILlmProvider> ProviderWithThreeToolCalls()
    {
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new ToolCall { Id = "call_a", Name = "tool_a", ArgumentsJson = "{}" },
                        new ToolCall { Id = "call_b", Name = "tool_b", ArgumentsJson = "{}" },
                        new ToolCall { Id = "call_c", Name = "tool_c", ArgumentsJson = "{}" },
                    },
                },
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "All done." },
                FinishReason = "stop",
            });
        return provider;
    }

    [Fact]
    public async Task MultipleToolCalls_RunConcurrently_ResultsStayInOriginalOrder()
    {
        var provider = ProviderWithThreeToolCalls();
        var completionOrder = new ConcurrentQueue<string>();

        // Delays are inverted relative to call order so completion order != call order:
        // tool_a (first) takes longest, tool_c (last) finishes first.
        var delays = new Dictionary<string, int>
        {
            ["tool_a"] = 120,
            ["tool_b"] = 60,
            ["tool_c"] = 10,
        };

        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .Returns<string, Dictionary<string, object?>, ToolExecutionContext>(async (name, _, ctx) =>
            {
                await Task.Delay(delays[name], ctx.CancellationToken);
                completionOrder.Enqueue(name);
                return new ToolExecutionResult
                {
                    IsSuccessful = true,
                    Data = $"data-from-{name}",
                    Message = $"{name} ok",
                };
            });

        var agent = NewAgent(provider.Object, executor.Object);

        var result = await agent.ProcessMessageAsync("do three things");

        Assert.True(result.Success);

        // (a) all three ran
        Assert.Equal(3, completionOrder.Count);

        // They actually completed out of order (proving concurrency, not serial execution).
        Assert.Equal(new[] { "tool_c", "tool_b", "tool_a" }, completionOrder.ToArray());

        // (b) the recorded tool-result messages are in the ORIGINAL tool-call order.
        var history = agent.GetHistory();
        var toolResults = history
            .Where(m => m.Role == Role.Tool)
            .SelectMany(m => m.ToolResults!)
            .ToList();

        Assert.Equal(3, toolResults.Count);
        Assert.Equal(new[] { "call_a", "call_b", "call_c" }, toolResults.Select(r => r.CallId).ToArray());
        Assert.Equal(new[] { "tool_a", "tool_b", "tool_c" }, toolResults.Select(r => r.Name).ToArray());

        // The payload of each result must belong to its own call (no cross-wiring).
        Assert.Contains("data-from-tool_a", toolResults[0].ResultJson);
        Assert.Contains("data-from-tool_b", toolResults[1].ResultJson);
        Assert.Contains("data-from-tool_c", toolResults[2].ResultJson);
    }

    [Fact]
    public async Task FailingToolCall_IsIsolated_AsThatCallsErrorResult()
    {
        var provider = ProviderWithThreeToolCalls();

        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .Returns<string, Dictionary<string, object?>, ToolExecutionContext>(async (name, _, ctx) =>
            {
                await Task.Delay(name == "tool_b" ? 10 : 50, ctx.CancellationToken);
                if (name == "tool_b")
                    throw new InvalidOperationException("boom from tool_b");

                return new ToolExecutionResult
                {
                    IsSuccessful = true,
                    Data = $"data-from-{name}",
                    Message = $"{name} ok",
                };
            });

        var agent = NewAgent(provider.Object, executor.Object);

        // The whole run must still succeed; the failure becomes a tool-result, not a thrown run.
        var result = await agent.ProcessMessageAsync("do three things");
        Assert.True(result.Success);

        var toolResults = agent.GetHistory()
            .Where(m => m.Role == Role.Tool)
            .SelectMany(m => m.ToolResults!)
            .ToList();

        Assert.Equal(3, toolResults.Count);

        // Order is still original call order even though tool_b finished first.
        Assert.Equal(new[] { "call_a", "call_b", "call_c" }, toolResults.Select(r => r.CallId).ToArray());

        // (c) tool_b is the only error; siblings succeeded.
        Assert.False(toolResults[0].IsError);
        Assert.True(toolResults[1].IsError);
        Assert.False(toolResults[2].IsError);

        // The error result carries tool_b's own message.
        using var errDoc = JsonDocument.Parse(toolResults[1].ResultJson!);
        Assert.False(errDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("boom from tool_b", errDoc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Cancellation_From_A_Tool_Call_Propagates_Out()
    {
        var provider = ProviderWithThreeToolCalls();
        using var cts = new CancellationTokenSource();

        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .Returns<string, Dictionary<string, object?>, ToolExecutionContext>(async (name, _, ctx) =>
            {
                if (name == "tool_a")
                {
                    cts.Cancel();
                    cts.Token.ThrowIfCancellationRequested();
                }

                await Task.Delay(50, ctx.CancellationToken);
                return new ToolExecutionResult { IsSuccessful = true, Data = name, Message = "ok" };
            });

        var agent = NewAgent(provider.Object, executor.Object);

        // A cancellation in one call must surface as OperationCanceledException, not an error result.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => agent.ProcessMessageAsync("do three things", cts.Token));
    }
}
