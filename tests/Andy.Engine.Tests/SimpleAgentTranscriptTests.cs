using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;
using ToolCall = Andy.Model.Model.ToolCall;

namespace Andy.Engine.Tests;

/// <summary>
/// Transcript snapshot/restore (issue #32): a multi-turn tool-using conversation can be exported,
/// serialized, deserialized and restored into a fresh agent, whose next request receives the
/// restored history in the original order. Invalid snapshots fail deterministically without
/// mutating agent state, and snapshots contain conversation content only.
/// </summary>
public class SimpleAgentTranscriptTests
{
    private static Mock<IToolRegistry> CreateRegistry()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>
        {
            new()
            {
                IsEnabled = true,
                Metadata = new ToolMetadata { Id = "read_file", Name = "Read File", Description = "Reads." },
            },
        });
        return registry;
    }

    private static Mock<IToolExecutor> CreateExecutor()
    {
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "contents of the file" });
        return executor;
    }

    private static SimpleAgent CreateAgent(ILlmProvider provider) =>
        new(provider, CreateRegistry().Object, CreateExecutor().Object, "system", maxTurns: 5);

    /// <summary>Drives one tool-using turn and one plain turn through a real agent.</summary>
    private static async Task<SimpleAgent> BuildConversationAsync()
    {
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = "checking...",
                    ToolCalls = new List<ToolCall> { new() { Id = "c1", Name = "read_file", ArgumentsJson = "{\"path\":\"a\"}" } },
                },
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "The file says hi." },
                FinishReason = "stop",
            })
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "You're welcome." },
                FinishReason = "stop",
            });

        var agent = CreateAgent(provider.Object);
        (await agent.ProcessMessageAsync("read file a")).Success.Should().BeTrue();
        (await agent.ProcessMessageAsync("thanks")).Success.Should().BeTrue();
        return agent;
    }

    [Fact]
    public async Task ToolConversation_RoundTrips_ThroughJson()
    {
        var original = await BuildConversationAsync();

        var json = original.ExportTranscript().ToJson();
        var snapshot = TranscriptSnapshot.FromJson(json);

        var restoredAgent = CreateAgent(Mock.Of<ILlmProvider>());
        restoredAgent.RestoreTranscript(snapshot);

        // The restored flat history matches the original in order, content and tool wiring.
        var expected = original.GetHistory();
        var actual = restoredAgent.GetHistory();
        actual.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            actual[i].Role.Should().Be(expected[i].Role, $"message {i}");
            actual[i].Content.Should().Be(expected[i].Content, $"message {i}");
            (actual[i].ToolCalls?.Count ?? 0).Should().Be(expected[i].ToolCalls?.Count ?? 0, $"message {i}");
            (actual[i].ToolResults?.Count ?? 0).Should().Be(expected[i].ToolResults?.Count ?? 0, $"message {i}");
        }
        actual.First(m => m.Role == Role.Tool).ToolResults![0].CallId.Should().Be("c1");

        // A second export round-trips identically (stable format).
        restoredAgent.ExportTranscript().ToJson().Should().Be(json);
    }

    [Fact]
    public async Task RestoredHistory_ReachesTheNextModelRequest_InOrder()
    {
        var original = await BuildConversationAsync();
        var snapshot = original.ExportTranscript();

        LlmRequest? captured = null;
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "continuing" },
                FinishReason = "stop",
            });

        var restored = CreateAgent(provider.Object);
        restored.RestoreTranscript(snapshot);
        await restored.ProcessMessageAsync("and now?");

        captured.Should().NotBeNull();
        var contents = captured!.Messages.Select(m => m.Content).ToList();
        contents.Should().ContainInOrder(
            "read file a", "checking...", "The file says hi.", "thanks", "You're welcome.", "and now?");
    }

    [Fact]
    public void EmptyHistory_ExportsAndRestores()
    {
        var agent = CreateAgent(Mock.Of<ILlmProvider>());
        var snapshot = agent.ExportTranscript();
        snapshot.Turns.Should().BeEmpty();

        var other = CreateAgent(Mock.Of<ILlmProvider>());
        other.RestoreTranscript(TranscriptSnapshot.FromJson(snapshot.ToJson()));
        other.Conversation.Turns.Should().BeEmpty();
    }

    [Fact]
    public async Task InterruptedTurn_WithNullFinalAnswer_RoundTrips()
    {
        // An LLM failure after a tool round commits a partial turn (issue #37); it must snapshot.
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = "",
                    ToolCalls = new List<ToolCall> { new() { Id = "c1", Name = "read_file", ArgumentsJson = "{}" } },
                },
            })
            .ThrowsAsync(new InvalidOperationException("boom"));

        var agent = CreateAgent(provider.Object);
        (await agent.ProcessMessageAsync("try it")).Success.Should().BeFalse();

        var snapshot = TranscriptSnapshot.FromJson(agent.ExportTranscript().ToJson());
        snapshot.Turns.Should().ContainSingle().Which.FinalAssistant.Should().BeNull();

        var restored = CreateAgent(Mock.Of<ILlmProvider>());
        restored.RestoreTranscript(snapshot);
        restored.Conversation.Turns.Single().ToolMessages.Should().HaveCount(2);
    }

    [Fact]
    public void UnsupportedVersion_IsRejected()
    {
        var act = () => TranscriptSnapshot.FromJson("{\"version\":99,\"turns\":[]}");
        act.Should().Throw<NotSupportedException>().WithMessage("*99*");
    }

    [Fact]
    public void InvalidRole_IsRejected_WithoutMutation()
    {
        var snapshot = new TranscriptSnapshot
        {
            Turns = new[]
            {
                new TranscriptTurn
                {
                    User = new TranscriptMessage { Role = "user", Content = "ok" },
                },
                new TranscriptTurn
                {
                    User = new TranscriptMessage { Role = "wizard", Content = "cast" },
                },
            },
        };

        var agent = CreateAgent(Mock.Of<ILlmProvider>());
        var act = () => agent.RestoreTranscript(snapshot);

        act.Should().Throw<ArgumentException>().WithMessage("*wizard*");
        agent.Conversation.Turns.Should().BeEmpty("a failed restore must not partially mutate state");
    }

    [Fact]
    public void BrokenToolCorrelation_IsRejected()
    {
        var snapshot = new TranscriptSnapshot
        {
            Turns = new[]
            {
                new TranscriptTurn
                {
                    User = new TranscriptMessage { Role = "user", Content = "go" },
                    Interleaved = new[]
                    {
                        new TranscriptMessage
                        {
                            Role = "tool",
                            ToolResults = new[]
                            {
                                new TranscriptToolResult { CallId = "orphan", ResultJson = "{}" },
                            },
                        },
                    },
                },
            },
        };

        var agent = CreateAgent(Mock.Of<ILlmProvider>());
        var act = () => agent.RestoreTranscript(snapshot);

        act.Should().Throw<ArgumentException>().WithMessage("*orphan*");
        agent.Conversation.Turns.Should().BeEmpty();
    }

    [Fact]
    public async Task RestoreIntoNonEmptyAgent_IsRejected()
    {
        var populated = await BuildConversationAsync();
        var snapshot = populated.ExportTranscript();

        var act = () => populated.RestoreTranscript(snapshot);
        act.Should().Throw<InvalidOperationException>().WithMessage("*empty*");
    }

    [Fact]
    public async Task Snapshot_ContainsNoAgentInfrastructure()
    {
        var agent = await BuildConversationAsync();
        var json = agent.ExportTranscript().ToJson();

        // The serialized form is conversation content only: no provider/executor/logger state.
        json.Should().NotContainAny("Provider", "Executor", "Logger", "CancellationToken", "ApiKey", "api_key");
        json.Should().Contain("\"version\":1");
    }
}
