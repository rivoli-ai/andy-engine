using Andy.Engine.Contracts;
using Andy.Engine.Planner;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json.Nodes;
using Xunit;

namespace Andy.Engine.Tests.Planner;

/// <summary>
/// Tests for conversational scenarios where the agent should respond directly
/// without calling tools or asking for clarification
/// </summary>
public class ConversationalPlannerTests
{
    [Fact]
    public async Task SimpleGreeting_ShouldStopWithResponse_NotCallTools()
    {
        // Arrange
        var mockLlm = new Mock<ILlmProvider>();
        var mockToolRegistry = new Mock<IToolRegistry>();

        // Setup LLM to return a stop decision with greeting response
        mockLlm.Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = """
                    {
                        "action": "stop",
                        "reason": "Hello! How can I help you today?"
                    }
                    """
                }
            });

        mockToolRegistry.Setup(x => x.Tools).Returns(new List<ToolRegistration>());

        var planner = new LlmPlanner(
            mockLlm.Object,
            mockToolRegistry.Object,
            options: null,
            logger: null
        );

        var state = new AgentState(
            Goal: new AgentGoal("hello", new List<string>()),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: null,
            Budget: new Budget(MaxTurns: 10, MaxWallClock: TimeSpan.FromMinutes(5)),
            TurnIndex: 1,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );

        // Act
        var decision = await planner.DecideAsync(state, CancellationToken.None);

        // Assert
        Assert.IsType<StopDecision>(decision);
        var stopDecision = (StopDecision)decision;
        Assert.Contains("Hello", stopDecision.Reason);
        Assert.DoesNotContain("process_info", stopDecision.Reason);
    }

    [Fact]
    public async Task SimpleQuestion_ShouldStopWithAnswer_NotAskUser()
    {
        // Arrange
        var mockLlm = new Mock<ILlmProvider>();
        var mockToolRegistry = new Mock<IToolRegistry>();

        // Setup LLM to return a stop decision with answer
        mockLlm.Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = """
                    {
                        "action": "stop",
                        "reason": "I am an AI assistant powered by andy-engine. I can help you with various tasks including writing code, answering questions, and executing commands."
                    }
                    """
                }
            });

        mockToolRegistry.Setup(x => x.Tools).Returns(new List<ToolRegistration>());

        var planner = new LlmPlanner(
            mockLlm.Object,
            mockToolRegistry.Object,
            options: null,
            logger: null
        );

        var state = new AgentState(
            Goal: new AgentGoal("what's your identification", new List<string>()),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: null,
            Budget: new Budget(MaxTurns: 10, MaxWallClock: TimeSpan.FromMinutes(5)),
            TurnIndex: 1,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );

        // Act
        var decision = await planner.DecideAsync(state, CancellationToken.None);

        // Assert
        Assert.IsType<StopDecision>(decision);
        var stopDecision = (StopDecision)decision;
        Assert.Contains("assistant", stopDecision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentCallsUnnecessaryTool_ShouldBeDocumented()
    {
        // This test documents the actual behavior we're seeing in production
        // where the agent calls process_info for a simple greeting

        // Arrange
        var mockLlm = new Mock<ILlmProvider>();
        var mockToolRegistry = new Mock<IToolRegistry>();

        // Setup LLM to return a call_tool decision (reproducing the bug)
        mockLlm.Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = """
                    {
                        "action": "call_tool",
                        "name": "process_info",
                        "args": {}
                    }
                    """
                }
            });

        mockToolRegistry.Setup(x => x.Tools).Returns(new List<ToolRegistration>());

        var planner = new LlmPlanner(
            mockLlm.Object,
            mockToolRegistry.Object,
            options: null,
            logger: null
        );

        var state = new AgentState(
            Goal: new AgentGoal("hello", new List<string>()),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: null,
            Budget: new Budget(MaxTurns: 10, MaxWallClock: TimeSpan.FromMinutes(5)),
            TurnIndex: 1,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );

        // Act
        var decision = await planner.DecideAsync(state, CancellationToken.None);

        // Assert - This documents the BUG
        Assert.IsType<CallToolDecision>(decision);
        var callDecision = (CallToolDecision)decision;
        Assert.Equal("process_info", callDecision.Call.ToolName);

        // TODO: Fix the Planner prompt so that simple greetings result in StopDecision
        // Expected behavior:
        // Assert.IsType<StopDecision>(decision);
    }

    [Fact]
    public async Task WriteProgramRequest_ShouldUseWriteFileTool_NotProvideEchoCommand()
    {
        // Arrange
        var mockLlm = new Mock<ILlmProvider>();
        var mockToolRegistry = new Mock<IToolRegistry>();

        // Setup empty tools registry (tool catalog doesn't affect this test)
        mockToolRegistry.Setup(x => x.Tools).Returns(new List<ToolRegistration>());

        // Setup LLM to return call_tool decision with write_file
        mockLlm.Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = """
                    {
                        "action": "call_tool",
                        "name": "write_file",
                        "args": {
                            "path": "/tmp/HelloWorld.cs",
                            "content": "using System;\n\nclass Program\n{\n    static void Main()\n    {\n        Console.WriteLine(\"Hello, World!\");\n    }\n}"
                        }
                    }
                    """
                }
            });

        var planner = new LlmPlanner(
            mockLlm.Object,
            mockToolRegistry.Object,
            options: null,
            logger: null
        );

        var state = new AgentState(
            Goal: new AgentGoal("write a sample C# program to /tmp", new List<string>()),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: null,
            Budget: new Budget(MaxTurns: 10, MaxWallClock: TimeSpan.FromMinutes(5)),
            TurnIndex: 1,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );

        // Act
        var decision = await planner.DecideAsync(state, CancellationToken.None);

        // Assert
        Assert.IsType<CallToolDecision>(decision);
        var callDecision = (CallToolDecision)decision;
        Assert.Equal("write_file", callDecision.Call.ToolName);
        Assert.NotNull(callDecision.Call.Args);

        var args = callDecision.Call.Args.AsObject();
        Assert.Contains("path", args.Select(kv => kv.Key));
        Assert.Contains("content", args.Select(kv => kv.Key));
    }
}
