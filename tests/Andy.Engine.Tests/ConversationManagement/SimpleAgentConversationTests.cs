using Andy.Model.Conversation;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andy.Engine.Tests.ConversationManagement;

public class SimpleAgentConversationTests
{
    private readonly Mock<ILlmProvider> _mockLlm;
    private readonly Mock<IToolRegistry> _mockRegistry;
    private readonly Mock<IToolExecutor> _mockExecutor;

    public SimpleAgentConversationTests()
    {
        _mockLlm = new Mock<ILlmProvider>();
        _mockRegistry = new Mock<IToolRegistry>();
        _mockExecutor = new Mock<IToolExecutor>();

        // Default: empty tool registry
        _mockRegistry.Setup(r => r.Tools)
            .Returns(Array.Empty<ToolRegistration>());
    }

    private SimpleAgent CreateAgent(IConversationManager? conversationManager = null)
    {
        return new SimpleAgent(
            _mockLlm.Object,
            _mockRegistry.Object,
            _mockExecutor.Object,
            systemPrompt: "You are a test assistant.",
            maxTurns: 10,
            workingDirectory: "/tmp",
            conversationManager: conversationManager);
    }

    /// <summary>
    /// Configure the mock LLM to return a simple text response (no tool calls).
    /// </summary>
    private void SetupSimpleResponse(string content = "Hello!")
    {
        _mockLlm.Setup(l => l.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = content
                },
                FinishReason = "stop"
            });
    }

    /// <summary>
    /// Configure the mock LLM to first return a tool call, then a final response.
    /// </summary>
    private void SetupToolCallThenResponse(
        string toolName = "test_tool",
        string toolArgsJson = "{}",
        string finalContent = "Done!")
    {
        var callCount = 0;
        _mockLlm.Setup(l => l.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall
                                {
                                    Id = "call_1",
                                    Name = toolName,
                                    ArgumentsJson = toolArgsJson
                                }
                            }
                        }
                    };
                }

                return new LlmResponse
                {
                    AssistantMessage = new Message
                    {
                        Role = Role.Assistant,
                        Content = finalContent
                    },
                    FinishReason = "stop"
                };
            });

        _mockExecutor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult
            {
                IsSuccessful = true,
                Data = "tool result data",
                Message = "Tool executed successfully"
            });
    }

    [Fact]
    public void DefaultConversationManager_IsUsed_WhenNotProvided()
    {
        var agent = CreateAgent();

        agent.ConversationManager.Should().NotBeNull();
        agent.ConversationManager.Should().BeOfType<DefaultConversationManager>();
    }

    [Fact]
    public void ConversationManager_IsUsed_WhenProvided()
    {
        var mockManager = new Mock<IConversationManager>();
        mockManager.Setup(m => m.Conversation).Returns(new Conversation());
        mockManager.Setup(m => m.ExtractMessagesForNextTurn()).Returns(Enumerable.Empty<Message>());

        var agent = CreateAgent(mockManager.Object);

        agent.ConversationManager.Should().BeSameAs(mockManager.Object);
    }

    [Fact]
    public async Task ConversationManager_AddTurn_IsCalledAfterProcessMessage()
    {
        var mockManager = new Mock<IConversationManager>();
        mockManager.Setup(m => m.Conversation).Returns(new Conversation());
        mockManager.Setup(m => m.ExtractMessagesForNextTurn()).Returns(Enumerable.Empty<Message>());

        SetupSimpleResponse("Test response");
        var agent = CreateAgent(mockManager.Object);

        await agent.ProcessMessageAsync("Hello");

        mockManager.Verify(m => m.AddTurn(It.IsAny<Turn>()), Times.Once);
    }

    [Fact]
    public async Task Conversation_AccumulatesTurns_AcrossMultipleCalls()
    {
        SetupSimpleResponse("Response");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("First message");
        await agent.ProcessMessageAsync("Second message");

        agent.Conversation.Turns.Should().HaveCount(2);
    }

    [Fact]
    public async Task Conversation_ContainsUserMessages()
    {
        SetupSimpleResponse("Response");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("What is the weather?");

        agent.Conversation.Turns.Should().HaveCount(1);
        var turn = agent.Conversation.Turns[0];
        turn.UserOrSystemMessage.Should().NotBeNull();
        turn.UserOrSystemMessage.Role.Should().Be(Role.User);
        turn.UserOrSystemMessage.Content.Should().Be("What is the weather?");
    }

    [Fact]
    public async Task Conversation_ContainsAssistantMessage()
    {
        SetupSimpleResponse("It is sunny!");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("What is the weather?");

        var turn = agent.Conversation.Turns[0];
        turn.AssistantMessage.Should().NotBeNull();
        turn.AssistantMessage.Role.Should().Be(Role.Assistant);
        turn.AssistantMessage.Content.Should().Be("It is sunny!");
    }

    [Fact]
    public async Task Conversation_ContainsToolMessages()
    {
        SetupToolCallThenResponse("test_tool", "{}", "Done with tools!");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Use a tool");

        var turn = agent.Conversation.Turns[0];
        turn.ToolMessages.Should().NotBeNullOrEmpty();
        turn.ToolMessages.Should().HaveCount(1);
        turn.ToolMessages[0].Role.Should().Be(Role.Tool);
    }

    [Fact]
    public async Task ClearHistory_ResetsConversation()
    {
        SetupSimpleResponse("Response");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("First message");
        agent.Conversation.Turns.Should().HaveCount(1);

        agent.ClearHistory();

        agent.Conversation.Turns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistory_ReturnsChronologicalMessages()
    {
        SetupSimpleResponse("Response 1");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Message 1");

        var history = agent.GetHistory();
        history.Should().NotBeEmpty();

        // Should have user message then assistant message
        history[0].Role.Should().Be(Role.User);
        history[0].Content.Should().Be("Message 1");
        history[1].Role.Should().Be(Role.Assistant);
        history[1].Content.Should().Be("Response 1");
    }

    [Fact]
    public async Task ConversationStats_AreAccurate()
    {
        SetupSimpleResponse("Response");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Message 1");
        await agent.ProcessMessageAsync("Message 2");

        var stats = agent.ConversationManager.GetStatistics();
        stats.TotalTurns.Should().Be(2);
        stats.UserMessages.Should().Be(2);
        stats.AssistantMessages.Should().Be(2);
    }

    [Fact]
    public async Task CustomConversationManager_ExtractMessagesForNextTurn_IsUsed()
    {
        var extractCalled = false;
        var mockManager = new Mock<IConversationManager>();
        mockManager.Setup(m => m.Conversation).Returns(new Conversation());
        mockManager.Setup(m => m.ExtractMessagesForNextTurn())
            .Returns(() =>
            {
                extractCalled = true;
                return Enumerable.Empty<Message>();
            });

        SetupSimpleResponse("Response");
        var agent = CreateAgent(mockManager.Object);

        await agent.ProcessMessageAsync("Hello");

        extractCalled.Should().BeTrue();
        mockManager.Verify(m => m.ExtractMessagesForNextTurn(), Times.Once);
    }
}
