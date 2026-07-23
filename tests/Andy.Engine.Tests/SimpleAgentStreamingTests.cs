using System.Runtime.CompilerServices;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;
using ToolCall = Andy.Model.Model.ToolCall;

namespace Andy.Engine.Tests;

/// <summary>
/// Incremental final-response streaming (issue #33): with a delta callback, provider text deltas
/// are forwarded in order tagged by turn; a turn that ends in tool calls (or truncation) gets a
/// Discarded event so its narration is never mislabeled as final text; non-streaming providers
/// emit exactly one immediate final chunk; the completed SimpleAgentResult stays fully populated.
/// </summary>
public class SimpleAgentStreamingTests
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
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "file contents" });
        return executor;
    }

    private static LlmStreamResponse TextChunk(string text) => new()
    {
        Delta = new Message { Role = Role.Assistant, Content = text },
    };

    private static LlmStreamResponse ToolCallChunk(string callId) => new()
    {
        Delta = new Message
        {
            Role = Role.Assistant,
            ToolCalls = new List<ToolCall> { new() { Id = callId, Name = "read_file", ArgumentsJson = "{}" } },
        },
    };

    private static LlmStreamResponse FinalChunk(string finishReason) => new()
    {
        IsComplete = true,
        FinishReason = finishReason,
    };

    private static async IAsyncEnumerable<LlmStreamResponse> Stream(
        IEnumerable<LlmStreamResponse> chunks,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return chunk;
        }
    }

    [Fact]
    public async Task StreamingProvider_ForwardsOrderedDeltas_AndPopulatesResult()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken ct) =>
                Stream(new[] { TextChunk("Hel"), TextChunk("lo"), FinalChunk("stop") }, ct));

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var deltas = new List<AgentResponseDelta>();
        var result = await agent.ProcessMessageAsync("hi", deltas.Add);

        result.Success.Should().BeTrue();
        result.Response.Should().Be("Hello");
        deltas.Should().HaveCount(2);
        deltas.Select(d => d.Text).Should().ContainInOrder("Hel", "lo");
        deltas.Should().OnlyContain(d => d.Kind == AgentResponseDeltaKind.Text && d.Turn == 1);

        // The completed turn is recorded exactly as in the non-streaming path.
        agent.Conversation.Turns.Should().ContainSingle()
            .Which.AssistantMessage!.Content.Should().Be("Hello");
        provider.Verify(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToolRoundNarration_IsDiscarded_FinalTurnIsNot()
    {
        var provider = new Mock<ILlmProvider>();
        var call = 0;
        provider.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken ct) =>
            {
                call++;
                return call == 1
                    ? Stream(new[]
                    {
                        TextChunk("Let me read that file."),
                        ToolCallChunk("c1"),
                        FinalChunk("tool_calls"),
                    }, ct)
                    : Stream(new[] { TextChunk("The answer is 42."), FinalChunk("stop") }, ct);
            });

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var deltas = new List<AgentResponseDelta>();
        var result = await agent.ProcessMessageAsync("read it", deltas.Add);

        result.Success.Should().BeTrue();
        result.Response.Should().Be("The answer is 42.");

        deltas.Should().HaveCount(3);
        deltas[0].Should().Match<AgentResponseDelta>(d =>
            d.Kind == AgentResponseDeltaKind.Text && d.Turn == 1 && d.Text == "Let me read that file.");
        deltas[1].Should().Match<AgentResponseDelta>(d =>
            d.Kind == AgentResponseDeltaKind.Discarded && d.Turn == 1);
        deltas[2].Should().Match<AgentResponseDelta>(d =>
            d.Kind == AgentResponseDeltaKind.Text && d.Turn == 2 && d.Text == "The answer is 42.");
    }

    [Fact]
    public async Task WithoutCallback_UsesNonStreamingPath()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "plain" },
                FinishReason = "stop",
            });

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var result = await agent.ProcessMessageAsync("hi");

        result.Response.Should().Be("plain");
        provider.Verify(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonStreamingProvider_FallsBack_AndEmitsSingleFinalChunk()
    {
        static async IAsyncEnumerable<LlmStreamResponse> Unsupported(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            throw new NotSupportedException("no streaming");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken ct) => Unsupported(ct));
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Message { Role = Role.Assistant, Content = "complete answer" },
                FinishReason = "stop",
            });

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var deltas = new List<AgentResponseDelta>();
        var result = await agent.ProcessMessageAsync("hi", deltas.Add);

        result.Response.Should().Be("complete answer");
        var single = deltas.Should().ContainSingle().Subject;
        single.Kind.Should().Be(AgentResponseDeltaKind.Text);
        single.Text.Should().Be("complete answer");
    }

    [Fact]
    public async Task ErrorChunk_FailsTheTurn_LikeANonStreamingError()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken ct) =>
                Stream(new[]
                {
                    new LlmStreamResponse { IsComplete = true, Error = "provider failed (status 400)" },
                }, ct));

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var deltas = new List<AgentResponseDelta>();
        var result = await agent.ProcessMessageAsync("hi", deltas.Add);

        result.Success.Should().BeFalse();
        result.StopReason.Should().Contain("provider failed");
        deltas.Should().BeEmpty("no text arrived before the error");
        // The interrupted turn is still committed (issue #37 semantics).
        agent.Conversation.Turns.Should().ContainSingle();
    }

    [Fact]
    public async Task CancellationMidStream_Throws_AndDeliversNoFurtherDeltas()
    {
        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<LlmStreamResponse> CancellingStream(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return TextChunk("part");
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            yield return TextChunk("never delivered");
        }

        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmRequest _, CancellationToken ct) => CancellingStream(ct));

        var agent = new SimpleAgent(provider.Object, CreateRegistry().Object, CreateExecutor().Object,
            "system", maxTurns: 5);

        var deltas = new List<AgentResponseDelta>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.ProcessMessageAsync("hi", deltas.Add, cts.Token));

        deltas.Should().ContainSingle().Which.Text.Should().Be("part");
    }
}
