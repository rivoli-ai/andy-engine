using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Cancellation must propagate out of <see cref="SimpleAgent.ProcessMessageAsync"/> as an
/// <see cref="OperationCanceledException"/> rather than being masked as a failed
/// <see cref="SimpleAgentResult"/>. Consumers (e.g. the headless cancel protocol in andy-cli)
/// rely on this to distinguish "cancelled" from "errored".
/// </summary>
public class SimpleAgentCancellationTests
{
    private static SimpleAgent NewAgent(ILlmProvider provider)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(
            provider,
            registry.Object,
            Mock.Of<IToolExecutor>(),
            systemPrompt: "system",
            maxTurns: 10);
    }

    [Fact]
    public async Task Cancellation_During_Completion_Propagates_Instead_Of_Failed_Result()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The provider observes the cancellation token and throws, exactly as a real
        // provider would when the request is cancelled.
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmRequest, CancellationToken>((_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new LlmResponse
                {
                    AssistantMessage = new Message { Role = Role.Assistant, Content = "unreachable" },
                    FinishReason = "stop",
                });
            });

        var agent = NewAgent(provider.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => agent.ProcessMessageAsync("Fix the bug.", cts.Token));
    }
}
