using Andy.Model.Conversation;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;
using ToolResult = Andy.Model.Model.ToolResult;

namespace Andy.Engine.Tests;

/// <summary>
/// Verifies that the per-request context VIEW is token-budgeted/compressed via Andy.Context
/// (SmartCompressor) while the full conversation log in the conversation manager is retained
/// unmodified.
/// </summary>
public class SimpleAgentContextCompactionTests
{
    private readonly Mock<ILlmProvider> _mockLlm = new();
    private readonly Mock<IToolRegistry> _mockRegistry = new();
    private readonly Mock<IToolExecutor> _mockExecutor = new();

    public SimpleAgentContextCompactionTests()
    {
        _mockRegistry.Setup(r => r.Tools).Returns(Array.Empty<ToolRegistration>());
    }

    private SimpleAgent CreateAgent(Conversation conversation, int maxContextTokens)
    {
        var manager = new Mock<IConversationManager>();
        manager.Setup(m => m.Conversation).Returns(conversation);
        return new SimpleAgent(
            _mockLlm.Object,
            _mockRegistry.Object,
            _mockExecutor.Object,
            systemPrompt: "You are a test assistant.",
            maxTurns: 10,
            workingDirectory: "/tmp",
            conversationManager: manager.Object,
            maxContextTokens: maxContextTokens);
    }

    /// <summary>Rough token estimate (≈ chars/4) used only to assert the budget was respected.</summary>
    private static int EstimateTokens(IEnumerable<Message> messages) =>
        messages.Sum(m => (m.Content?.Length ?? 0) / 4 + 4);

    /// <summary>
    /// Builds a turn: user message, an intermediate assistant(tool_call) + tool result, and a final
    /// assistant message. <paramref name="toolOutputChars"/> controls how heavy the tool result is.
    /// </summary>
    private static Turn MakeToolTurn(int index, int toolOutputChars)
    {
        var callId = $"call_{index}";
        return new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = $"User request {index}" },
            ToolMessages = new List<Message>
            {
                new Message
                {
                    Role = Role.Assistant,
                    Content = $"Calling tool for {index}",
                    ToolCalls = new List<ToolCall>
                    {
                        new ToolCall { Id = callId, Name = "read_file", ArgumentsJson = "{\"path\":\"x\"}" },
                    },
                },
                new Message
                {
                    Role = Role.Tool,
                    Content = new string('t', toolOutputChars) + $" result {index}",
                    ToolResults = new List<ToolResult>
                    {
                        new ToolResult { CallId = callId, Name = "read_file", ResultJson = "{}" },
                    },
                },
            },
            AssistantMessage = new Message { Role = Role.Assistant, Content = $"Done {index}" },
        };
    }

    [Fact]
    public void OversizedHistory_IsCompressedBelowBudget_FullLogRetained()
    {
        var conversation = new Conversation();
        // ~30 turns each carrying a huge tool output: far above any reasonable budget.
        for (var i = 0; i < 30; i++)
            conversation.AddTurn(MakeToolTurn(i, toolOutputChars: 40_000));

        const int budget = 10_000;
        var agent = CreateAgent(conversation, maxContextTokens: budget);

        var rawView = agent.GetHistory();          // uncompressed flat view of the full log
        var requestView = agent.BuildRequestContext();

        var rawTokens = EstimateTokens(rawView);
        var requestTokens = EstimateTokens(requestView);

        // Sanity: the raw history is genuinely oversized.
        rawTokens.Should().BeGreaterThan(budget * 5);

        // The compressed request view is much smaller and fits the budget.
        requestTokens.Should().BeLessThan(rawTokens);
        requestTokens.Should().BeLessThanOrEqualTo(budget);

        // The full conversation log is NOT mutated by building the request view.
        conversation.Turns.Should().HaveCount(30);
        EstimateTokens(agent.GetHistory()).Should().Be(rawTokens);
    }

    [Fact]
    public void SmallHistory_PassesThroughIntact()
    {
        var conversation = new Conversation();
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Hello there" },
            ToolMessages = new List<Message>(),
            AssistantMessage = new Message { Role = Role.Assistant, Content = "Hi! How can I help?" },
        });

        var agent = CreateAgent(conversation, maxContextTokens: 120_000);

        var rawView = agent.GetHistory();
        var requestView = agent.BuildRequestContext();

        // Same number of messages, same roles, same content — untouched.
        requestView.Should().HaveCount(rawView.Count);
        requestView.Select(m => m.Role).Should().Equal(rawView.Select(m => m.Role));
        requestView.Select(m => m.Content).Should().Equal(rawView.Select(m => m.Content));
        EstimateTokens(requestView).Should().Be(EstimateTokens(rawView));
    }

    [Fact]
    public void ToolCallResultPairs_ArePreserved_AfterCompression()
    {
        var conversation = new Conversation();
        for (var i = 0; i < 30; i++)
            conversation.AddTurn(MakeToolTurn(i, toolOutputChars: 40_000));

        var agent = CreateAgent(conversation, maxContextTokens: 10_000);

        var requestView = agent.BuildRequestContext();

        // Every tool result in the compressed view is backed by an assistant tool_call with the
        // same id present in the view (no orphaned tool results).
        var callIds = requestView
            .Where(m => m.ToolCalls is { Count: > 0 })
            .SelectMany(m => m.ToolCalls!)
            .Select(tc => tc.Id)
            .ToHashSet();

        var toolResults = requestView
            .Where(m => m.ToolResults is { Count: > 0 })
            .SelectMany(m => m.ToolResults!)
            .ToList();

        toolResults.Should().NotBeEmpty("compression should retain some recent tool results");
        toolResults.Should().OnlyContain(tr => callIds.Contains(tr.CallId),
            "every surviving tool result must be paired with its triggering assistant tool_call");
    }
}
