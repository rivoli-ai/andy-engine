using System.Net;
using System.Runtime.CompilerServices;
using Andy.Llm.Errors;
using Andy.Model.Llm;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

public class SimpleAgentProviderErrorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainsTypedProviderFailureOnCompleteAndStreamingPaths(bool stream)
    {
        var details = new LlmProviderError("Moonshot AI", 429, 1, "Try again shortly.");
        var provider = new Mock<ILlmProvider>();
        provider.SetupGet(p => p.Name).Returns("openrouter");
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmProviderException(details, new InvalidOperationException("private raw envelope")));
        provider.Setup(p => p.StreamCompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns(FailingStream(details));
        using var agent = Agent(provider.Object);
        var result = stream ? await agent.ProcessMessageAsync("go", _ => { }) : await agent.ProcessMessageAsync("go");
        Assert.False(result.Success);
        Assert.Equal(details, result.ProviderError);
        Assert.Contains("Try again shortly", result.Response);
        Assert.DoesNotContain("private raw envelope", result.Response);
        Assert.DoesNotContain("private raw envelope", result.StopReason);
    }

    [Fact]
    public async Task NormalizesUntypedNetworkExceptionAtProviderBoundary()
    {
        var provider = new Mock<ILlmProvider>();
        provider.SetupGet(p => p.Name).Returns("openai");
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));
        using var agent = Agent(provider.Object);
        var result = await agent.ProcessMessageAsync("go");
        Assert.Equal(503, result.ProviderError!.StatusCode);
        Assert.Equal("openai", result.ProviderError.Provider);
    }

    [Fact]
    public void ExistingResultConstructorAndDeconstructionRemainCompatible()
    {
        var result = new SimpleAgentResult(true, "ok", 1, TimeSpan.Zero, "completed");
        var (success, response, turns, duration, reason) = result;
        Assert.True(success);
        Assert.Equal("ok", response);
        Assert.Null(result.ProviderError);
    }

    private static SimpleAgent Agent(ILlmProvider provider)
    {
        var registry = new Mock<IToolRegistry>();
        registry.SetupGet(r => r.Tools).Returns(Array.Empty<ToolRegistration>());
        return new(provider, registry.Object, new Mock<IToolExecutor>().Object, "Respond.");
    }
    private static async IAsyncEnumerable<LlmStreamResponse> FailingStream(LlmProviderError error,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        if (error != null) throw new LlmProviderException(error);
        yield break;
    }
}
