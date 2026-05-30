using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// A turn cut off by the output-token limit (FinishReason "length"/"max_tokens") is the model
/// being interrupted mid-task, not a finished answer. The agent must continue rather than
/// returning a partial/empty result (which, in the SWE-bench harness, surfaces as an empty patch).
/// </summary>
public class SimpleAgentTruncationTests
{
    private static LlmResponse Response(string content, string finishReason) => new()
    {
        AssistantMessage = new Message { Role = Role.Assistant, Content = content },
        FinishReason = finishReason,
    };

    private static SimpleAgent NewAgent(ILlmProvider provider)
    {
        // No tools registered: the agent must drive purely off LLM responses.
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(
            provider,
            registry.Object,
            Mock.Of<IToolExecutor>(),
            systemPrompt: "system",
            maxTurns: 10,
            maxContextTokens: 200_000);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("max_tokens")]
    [InlineData("LENGTH")]
    public void IsTruncatedByOutputLimit_Detects_Truncation_FinishReasons(string finishReason)
    {
        Assert.True(SimpleAgent.IsTruncatedByOutputLimit(finishReason));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("tool_calls")]
    [InlineData(null)]
    public void IsTruncatedByOutputLimit_Ignores_NonTruncation_FinishReasons(string? finishReason)
    {
        Assert.False(SimpleAgent.IsTruncatedByOutputLimit(finishReason));
    }

    [Fact]
    public async Task Truncated_Turn_Continues_Instead_Of_Ending_With_Success()
    {
        // First turn: cut off by the output limit with empty content (no tool calls).
        // Second turn: a genuine final answer.
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(content: string.Empty, finishReason: "length"))
            .ReturnsAsync(Response(content: "All done.", finishReason: "stop"));

        var agent = NewAgent(provider.Object);

        var result = await agent.ProcessMessageAsync("Fix the bug.");

        // The agent must NOT have stopped on the truncated turn.
        Assert.True(result.Success);
        Assert.Equal("All done.", result.Response);
        Assert.Equal(2, result.TurnCount);
        Assert.Equal("stop", result.StopReason);
        provider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task NonTruncated_Empty_Response_Still_Ends_Immediately()
    {
        // Regression guard: a normal stop with no tool calls is a final response on turn 1.
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(content: "Nothing to change.", finishReason: "stop"));

        var agent = NewAgent(provider.Object);

        var result = await agent.ProcessMessageAsync("Fix the bug.");

        Assert.True(result.Success);
        Assert.Equal(1, result.TurnCount);
        provider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
