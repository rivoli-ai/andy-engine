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

    /// <summary>
    /// Configure the mock LLM to return two sequential tool calls, then a final response.
    /// Simulates: Assistant(tool_calls_1) → Tool(result_1) → Assistant(tool_calls_2) → Tool(result_2) → Assistant(final)
    /// </summary>
    private void SetupMultiStepToolCallsThenResponse(
        string finalContent = "All done!")
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
                            Content = "I'll read the file first.",
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall
                                {
                                    Id = "call_1",
                                    Name = "read_file",
                                    ArgumentsJson = "{\"path\": \"/tmp/test.txt\"}"
                                }
                            }
                        }
                    };
                }

                if (callCount == 2)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "Now I'll write the result.",
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall
                                {
                                    Id = "call_2",
                                    Name = "write_file",
                                    ArgumentsJson = "{\"path\": \"/tmp/output.txt\", \"content\": \"result\"}"
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

        var agent = CreateAgent(mockManager.Object);

        agent.ConversationManager.Should().BeSameAs(mockManager.Object);
    }

    [Fact]
    public async Task ConversationManager_AddTurn_IsCalledAfterProcessMessage()
    {
        var mockManager = new Mock<IConversationManager>();
        mockManager.Setup(m => m.Conversation).Returns(new Conversation());

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
    public async Task Conversation_ContainsToolMessages_WithIntermediateAssistant()
    {
        SetupToolCallThenResponse("test_tool", "{}", "Done with tools!");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Use a tool");

        var turn = agent.Conversation.Turns[0];
        turn.ToolMessages.Should().NotBeNullOrEmpty();
        // ToolMessages now contains both the intermediate assistant message (with ToolCalls) and the tool result
        turn.ToolMessages.Should().HaveCount(2);
        turn.ToolMessages[0].Role.Should().Be(Role.Assistant);
        turn.ToolMessages[0].ToolCalls.Should().NotBeNullOrEmpty();
        turn.ToolMessages[1].Role.Should().Be(Role.Tool);
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
    public async Task GetHistory_WithToolCalls_ReturnsCorrectOrder()
    {
        SetupToolCallThenResponse("test_tool", "{}", "Done!");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Use a tool");

        var history = agent.GetHistory();

        // Expected order: User → Assistant(tool_calls) → Tool(result) → Assistant(final)
        history.Should().HaveCount(4);
        history[0].Role.Should().Be(Role.User);
        history[1].Role.Should().Be(Role.Assistant);
        history[1].ToolCalls.Should().NotBeNullOrEmpty("intermediate assistant should have tool calls");
        history[2].Role.Should().Be(Role.Tool);
        history[3].Role.Should().Be(Role.Assistant);
        history[3].Content.Should().Be("Done!");
    }

    [Fact]
    public async Task GetHistory_WithMultiStepToolCalls_ReturnsCorrectOrder()
    {
        SetupMultiStepToolCallsThenResponse("All done!");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Do two things");

        var history = agent.GetHistory();

        // Expected: User → Asst(tool_calls_1) → Tool(result_1) → Asst(tool_calls_2) → Tool(result_2) → Asst(final)
        history.Should().HaveCount(6);
        history[0].Role.Should().Be(Role.User);
        history[1].Role.Should().Be(Role.Assistant);
        history[1].ToolCalls.Should().NotBeNullOrEmpty();
        history[1].ToolCalls[0].Name.Should().Be("read_file");
        history[2].Role.Should().Be(Role.Tool);
        history[3].Role.Should().Be(Role.Assistant);
        history[3].ToolCalls.Should().NotBeNullOrEmpty();
        history[3].ToolCalls[0].Name.Should().Be("write_file");
        history[4].Role.Should().Be(Role.Tool);
        history[5].Role.Should().Be(Role.Assistant);
        history[5].Content.Should().Be("All done!");
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
    public async Task MultiTurn_WithToolCalls_ContextIncludesIntermediateAssistantMessages()
    {
        // This test verifies the core bug fix: on the SECOND call to ProcessMessageAsync,
        // the context sent to the LLM must include intermediate assistant messages with ToolCalls
        // from the first call, so that tool result messages are properly preceded by their
        // triggering assistant message.

        var capturedRequests = new List<LlmRequest>();
        var callCount = 0;

        _mockLlm.Setup(l => l.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call: tool call
                if (callCount == 1)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "Let me check that.",
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall { Id = "call_1", Name = "test_tool", ArgumentsJson = "{}" }
                            }
                        }
                    };
                }
                // Second call: final response for first ProcessMessageAsync
                if (callCount == 2)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message { Role = Role.Assistant, Content = "First done." },
                        FinishReason = "stop"
                    };
                }
                // Third call: simple response for second ProcessMessageAsync
                return new LlmResponse
                {
                    AssistantMessage = new Message { Role = Role.Assistant, Content = "Second done." },
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
                Data = "result",
                Message = "OK"
            });

        var agent = CreateAgent();

        // First call (with tool call)
        await agent.ProcessMessageAsync("First question");

        // Second call (simple)
        await agent.ProcessMessageAsync("Second question");

        // The third LLM request (for second ProcessMessageAsync) should have context
        // from the first turn with proper message ordering
        capturedRequests.Should().HaveCount(3);
        var thirdRequest = capturedRequests[2];
        var messages = thirdRequest.Messages;

        // Context should be: [Turn1: User → Asst(tool_calls) → Tool → Asst(final)] + [Turn2: User]
        messages.Should().HaveCount(5);

        messages[0].Role.Should().Be(Role.User);
        messages[0].Content.Should().Be("First question");

        messages[1].Role.Should().Be(Role.Assistant);
        messages[1].ToolCalls.Should().NotBeNullOrEmpty("intermediate assistant must have tool_calls");

        messages[2].Role.Should().Be(Role.Tool);

        messages[3].Role.Should().Be(Role.Assistant);
        messages[3].Content.Should().Be("First done.");

        messages[4].Role.Should().Be(Role.User);
        messages[4].Content.Should().Be("Second question");
    }

    [Fact]
    public async Task MultiTurn_WithMultiStepToolCalls_ContextPreservesAllIntermediateMessages()
    {
        // Verifies that multi-step tool calls (2 rounds) in the first call
        // produce correct context for the second call

        var capturedRequests = new List<LlmRequest>();
        var callCount = 0;

        _mockLlm.Setup(l => l.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
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
                            Content = "Step 1",
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall { Id = "c1", Name = "tool_a", ArgumentsJson = "{}" }
                            }
                        }
                    };
                }
                if (callCount == 2)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "Step 2",
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall { Id = "c2", Name = "tool_b", ArgumentsJson = "{}" }
                            }
                        }
                    };
                }
                if (callCount == 3)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
                        FinishReason = "stop"
                    };
                }
                // Call 4: second ProcessMessageAsync
                return new LlmResponse
                {
                    AssistantMessage = new Message { Role = Role.Assistant, Content = "Follow-up done." },
                    FinishReason = "stop"
                };
            });

        _mockExecutor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok", Message = "ok" });

        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Do complex thing");
        await agent.ProcessMessageAsync("Follow up");

        // The 4th request should have full context from first turn
        capturedRequests.Should().HaveCount(4);
        var fourthRequest = capturedRequests[3];
        var msgs = fourthRequest.Messages;

        // Turn 1: User → Asst(c1) → Tool → Asst(c2) → Tool → Asst(final)
        // Turn 2: User
        msgs.Should().HaveCount(7);

        msgs[0].Role.Should().Be(Role.User);
        msgs[0].Content.Should().Be("Do complex thing");

        msgs[1].Role.Should().Be(Role.Assistant);
        msgs[1].ToolCalls.Should().ContainSingle(tc => tc.Name == "tool_a");

        msgs[2].Role.Should().Be(Role.Tool);

        msgs[3].Role.Should().Be(Role.Assistant);
        msgs[3].ToolCalls.Should().ContainSingle(tc => tc.Name == "tool_b");

        msgs[4].Role.Should().Be(Role.Tool);

        msgs[5].Role.Should().Be(Role.Assistant);
        msgs[5].Content.Should().Be("Done.");
        msgs[5].ToolCalls.Should().BeNullOrEmpty("final assistant should not have tool calls");

        msgs[6].Role.Should().Be(Role.User);
        msgs[6].Content.Should().Be("Follow up");
    }

    [Fact]
    public async Task NoToolCalls_TurnHasEmptyToolMessages()
    {
        SetupSimpleResponse("Simple response");
        var agent = CreateAgent();

        await agent.ProcessMessageAsync("Hello");

        var turn = agent.Conversation.Turns[0];
        turn.ToolMessages.Should().BeEmpty();
        turn.AssistantMessage.Content.Should().Be("Simple response");
    }
}
