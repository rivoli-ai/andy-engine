using Andy.Model.Conversation;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;
using ToolCall = Andy.Model.Model.ToolCall;

namespace Andy.Engine.Tests;

/// <summary>
/// A turn whose tools already executed has side effects on disk; if the turn is then lost to an
/// LLM error or cancellation, the next call's context contradicts reality and the model redoes or
/// undoes work (issue #37). Also covers ClearHistory keeping a caller-injected conversation
/// manager (issue #42).
/// </summary>
public class SimpleAgentTurnPersistenceTests
{
    private static Mock<IToolRegistry> CreateRegistry()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>
        {
            new()
            {
                IsEnabled = true,
                Metadata = new ToolMetadata { Id = "write_file", Name = "Write File", Description = "Writes." },
            },
        });
        return registry;
    }

    private static Mock<IToolExecutor> CreateExecutor()
    {
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "written" });
        return executor;
    }

    private static LlmResponse ToolCallResponse(string callId) => new()
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = "",
            ToolCalls = new List<ToolCall> { new() { Id = callId, Name = "write_file", ArgumentsJson = "{}" } },
        },
    };

    [Fact]
    public async Task LlmError_AfterToolRound_CommitsPartialTurn()
    {
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCallResponse("c1"))
            .ThrowsAsync(new InvalidOperationException("provider exploded (status 500)"));

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var result = await agent.ProcessMessageAsync("write the file");

        result.Success.Should().BeFalse();
        result.StopReason.Should().StartWith("error:");

        // The executed tool round survives in history even though the turn errored out.
        var turn = agent.Conversation.Turns.Should().ContainSingle().Subject;
        turn.UserOrSystemMessage!.Content.Should().Be("write the file");
        turn.AssistantMessage.Should().BeNull("the turn produced no final answer");
        turn.ToolMessages.Should().HaveCount(2, "assistant(tool_calls) + tool result");
        turn.ToolMessages![1].Role.Should().Be(Role.Tool);
    }

    [Fact]
    public async Task Cancellation_AfterToolRound_CommitsPartialTurn_AndStillThrows()
    {
        using var cts = new CancellationTokenSource();
        var provider = new Mock<ILlmProvider>();
        var call = 0;
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmRequest, CancellationToken>((_, ct) =>
            {
                call++;
                if (call == 1)
                    return Task.FromResult(ToolCallResponse("c1"));
                cts.Cancel();
                return Task.FromCanceled<LlmResponse>(new CancellationToken(true));
            });

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.ProcessMessageAsync("write it", cts.Token));

        var turn = agent.Conversation.Turns.Should().ContainSingle().Subject;
        turn.ToolMessages.Should().HaveCount(2, "the executed tool round must survive cancellation");
    }

    [Fact]
    public async Task ImmediateLlmError_CommitsUserMessageOnly()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var result = await agent.ProcessMessageAsync("hello");

        result.Success.Should().BeFalse();
        var turn = agent.Conversation.Turns.Should().ContainSingle().Subject;
        turn.UserOrSystemMessage!.Content.Should().Be("hello");
        turn.ToolMessages.Should().BeEmpty();
        turn.AssistantMessage.Should().BeNull();
    }

    [Fact]
    public async Task SuccessfulTurn_IsNotDoubleCommitted()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "hi" },
                FinishReason = "stop",
            });

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        await agent.ProcessMessageAsync("hello");

        agent.Conversation.Turns.Should().HaveCount(1);
    }

    [Fact]
    public void ClearHistory_KeepsInjectedConversationManager()
    {
        var manager = new Mock<IConversationManager>();
        manager.Setup(m => m.Conversation).Returns(new Conversation());

        var agent = new SimpleAgent(Mock.Of<ILlmProvider>(), CreateRegistry().Object, CreateExecutor().Object,
            "system", conversationManager: manager.Object);

        agent.ClearHistory();

        manager.Verify(m => m.Reset(), Times.Once);
        agent.ConversationManager.Should().BeSameAs(manager.Object,
            "a caller-supplied manager must never be silently replaced");
    }

    [Fact]
    public async Task ClearHistory_WithDefaultManager_EmptiesConversation()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "hi" },
                FinishReason = "stop",
            });
        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        await agent.ProcessMessageAsync("hello");
        agent.Conversation.Turns.Should().HaveCount(1);

        agent.ClearHistory();

        agent.Conversation.Turns.Should().BeEmpty();
    }
}
