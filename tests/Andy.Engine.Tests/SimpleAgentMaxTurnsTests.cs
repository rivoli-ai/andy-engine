using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Behavior when the agent runs out of turn budget:
///  - it returns a concise notice, NOT the full raw conversation-history dump (which callers used
///    to render verbatim, flooding their UI with tool JSON / CRLFs);
///  - as it approaches the budget it sends a one-time "wrap up" nudge so the model can finish
///    before the hard limit.
/// </summary>
public class SimpleAgentMaxTurnsTests
{
    private const string WrapUpMarker = "produce your final answer now";

    private static SimpleAgent NewAgent(ILlmProvider provider, IToolExecutor executor, int maxTurns)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(provider, registry.Object, executor, systemPrompt: "system", maxTurns: maxTurns);
    }

    // A provider that never finishes: every turn it emits a single tool call, forcing the agent
    // to loop until it exhausts its turn budget.
    private static Mock<ILlmProvider> NeverFinishingProvider(List<LlmRequest>? capture = null)
    {
        var provider = new Mock<ILlmProvider>();
        var setup = provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()));
        if (capture != null)
        {
            setup.Callback<LlmRequest, CancellationToken>((req, _) => capture.Add(req));
        }
        setup.ReturnsAsync(new LlmResponse
        {
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall { Id = "call_1", Name = "explore", ArgumentsJson = "{}" },
                },
            },
        });
        return provider;
    }

    private static IToolExecutor AlwaysSucceedingExecutor()
    {
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });
        return executor.Object;
    }

    [Theory]
    [InlineData(10, 7, false)]
    [InlineData(10, 8, true)]
    [InlineData(10, 10, true)]
    [InlineData(50, 39, false)]
    [InlineData(50, 40, true)]
    [InlineData(2, 2, false)]   // too small to nudge
    [InlineData(1, 1, false)]
    public void ShouldSendWrapUpNudge_FiresAtAboutEightyPercent(int maxTurns, int turn, bool expected)
    {
        Assert.Equal(expected, SimpleAgent.ShouldSendWrapUpNudge(turn, maxTurns));
    }

    [Fact]
    public async Task MaxTurns_ReturnsConciseNotice_NotHistoryDump()
    {
        var provider = NeverFinishingProvider();
        var agent = NewAgent(provider.Object, AlwaysSucceedingExecutor(), maxTurns: 3);

        var result = await agent.ProcessMessageAsync("Work on the task.");

        Assert.False(result.Success);
        Assert.Equal("max_turns_exceeded", result.StopReason);
        Assert.Equal(3, result.TurnCount);

        // The response must be a clean notice, not the old raw dump.
        Assert.DoesNotContain("Conversation history", result.Response);
        Assert.DoesNotContain("ToolCalls Count", result.Response);
        Assert.DoesNotContain("Role:", result.Response);
        Assert.Contains("maximum", result.Response);
        Assert.Contains("turns", result.Response);
    }

    [Fact]
    public async Task WrapUpNudge_IsSentOnce_AsAgentApproachesLimit()
    {
        var requests = new List<LlmRequest>();
        var provider = NeverFinishingProvider(requests);
        // maxTurns 5 -> nudge threshold = ceil(4) = turn 4.
        var agent = NewAgent(provider.Object, AlwaysSucceedingExecutor(), maxTurns: 5);

        await agent.ProcessMessageAsync("Work on the task.");

        // Early turns (before turn 4) must NOT contain the nudge.
        Assert.DoesNotContain(
            requests[0].Messages,
            m => (m.Content ?? "").Contains(WrapUpMarker));

        // The final request must contain the nudge exactly once (added once, then persists).
        var lastRequest = requests[^1];
        var nudgeCount = lastRequest.Messages.Count(m => (m.Content ?? "").Contains(WrapUpMarker));
        Assert.Equal(1, nudgeCount);
    }

    [Fact]
    public async Task WrapUpNudge_NotSent_WhenModelFinishesEarly()
    {
        var requests = new List<LlmRequest>();
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => requests.Add(req))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                FinishReason = "stop",
            });
        var agent = NewAgent(provider.Object, AlwaysSucceedingExecutor(), maxTurns: 10);

        var result = await agent.ProcessMessageAsync("Quick task.");

        Assert.True(result.Success);
        Assert.Equal(1, result.TurnCount);
        Assert.DoesNotContain(
            requests.SelectMany(r => r.Messages),
            m => (m.Content ?? "").Contains(WrapUpMarker));
    }
}
