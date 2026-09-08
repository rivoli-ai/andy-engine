using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

public class SimpleAgentPendingInputTests
{
    private static LlmResponse Tools() => new()
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            ToolCalls = new List<ToolCall>
        {
            new() { Id = "a", Name = "first", ArgumentsJson = "{}" },
            new() { Id = "b", Name = "second", ArgumentsJson = "{}" }
        }
        }
    };
    private static LlmResponse Done() => new() { AssistantMessage = new Message { Role = Role.Assistant, Content = "done" } };
    private static SimpleAgent Agent(ILlmProvider provider, IToolExecutor executor, int maxTurns = 10) =>
        new(provider, Mock.Of<IToolRegistry>(r => r.Tools == Array.Empty<ToolRegistration>()), executor, "system", maxTurns: maxTurns);

    [Fact]
    public async Task InputIsAcceptedAfterAllParallelToolsAndPreservedInTranscript()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int startCount = 0, polls = 0;
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Returns<string, Dictionary<string, object?>, ToolExecutionContext>(async (name, _, context) =>
            {
                if (Interlocked.Increment(ref startCount) == 2) started.TrySetResult();
                await (name == "first" ? first.Task : second.Task).WaitAsync(context.CancellationToken);
                return new ToolExecutionResult { IsSuccessful = true, Data = name };
            });
        var requests = new List<LlmRequest>();
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmRequest, CancellationToken>((request, _) => { requests.Add(request); return Task.FromResult(requests.Count == 1 ? Tools() : Done()); });
        var agent = Agent(provider.Object, executor.Object);
        agent.PendingInputProvider = _ =>
        {
            Interlocked.Increment(ref polls);
            Assert.True(first.Task.IsCompleted && second.Task.IsCompleted);
            return Task.FromResult<IReadOnlyList<IReadOnlyList<MessagePart>>>(new IReadOnlyList<MessagePart>[] { new MessagePart[] { new TextPart("change direction") }, new MessagePart[] { new TextPart("keep tests") } });
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = agent.ProcessMessageAsync("initial", deadline.Token);
        await started.Task.WaitAsync(deadline.Token);
        first.SetResult();
        Assert.Equal(0, Volatile.Read(ref polls));
        second.SetResult();
        Assert.True((await run).Success);
        Assert.Equal(1, polls);
        var context = requests[1].Messages;
        var correction = context.ToList().FindIndex(m => m.Content == "change direction");
        Assert.Equal(Role.Tool, context[correction - 1].Role);
        Assert.Equal("keep tests", context[correction + 1].Content);
        var restored = Agent(provider.Object, executor.Object);
        restored.RestoreTranscript(agent.ExportTranscript());
        Assert.Equal(new[] { "change direction", "keep tests" }, restored.GetHistory().Where(m => m.Role == Role.User && m.Content != "initial").Select(m => m.Content));
    }

    [Fact]
    public async Task PendingImageAndTextSurviveJsonTranscriptRoundTrip()
    {
        byte[] bytes = { 137, 80, 78, 71, 13, 10, 26, 10 };
        var provider = new Mock<IVisionCapableLlmProvider>();
        provider.Setup(p => p.SupportsImageInputAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var requests = new List<LlmRequest>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmRequest, CancellationToken>((request, _) => { requests.Add(request); return Task.FromResult(requests.Count == 1 ? Tools() : Done()); });
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>())).ReturnsAsync(new ToolExecutionResult { IsSuccessful = true });
        var agent = Agent(provider.Object, executor.Object);
        agent.PendingInputProvider = _ => Task.FromResult<IReadOnlyList<IReadOnlyList<MessagePart>>>(new IReadOnlyList<MessagePart>[]
        {
            new MessagePart[] { new TextPart("inspect image"), new ImagePart { MimeType = "image/png", ImageData = bytes } }
        });
        Assert.True((await agent.ProcessMessageAsync("initial")).Success);
        var message = requests[1].Messages.Single(m => m.Content == "inspect image");
        Assert.Equal(bytes, Assert.Single(MultimodalMessage.GetAttachedParts(message)!.OfType<ImagePart>()).ImageData);
        var restored = Agent(provider.Object, executor.Object);
        restored.RestoreTranscript(TranscriptSnapshot.FromJson(agent.ExportTranscript().ToJson()));
        Assert.Equal(bytes, Assert.Single(MultimodalMessage.GetAttachedParts(restored.GetHistory().Single(m => m.Content == "inspect image"))!.OfType<ImagePart>()).ImageData);
    }

    [Fact]
    public void RestoreRejectsUserInputInsertedBeforeToolResults()
    {
        var agent = Agent(Mock.Of<ILlmProvider>(), Mock.Of<IToolExecutor>());
        var snapshot = new TranscriptSnapshot
        {
            Turns = new[] { new TranscriptTurn
        {
            User = new TranscriptMessage { Role = "user", Content = "initial" },
            Interleaved = new[]
            {
                TranscriptMessage.FromMessage(Tools().AssistantMessage),
                new TranscriptMessage { Role = "user", Content = "too early" }
            }
        } }
        };
        Assert.Throws<ArgumentException>(() => agent.RestoreTranscript(snapshot));
        Assert.Empty(agent.GetHistory());
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 1)]
    public async Task FinalAnswerOrBudgetStopDoesNotDrainPendingInput(bool tools, int maxTurns)
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(tools ? Tools() : Done());
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>())).ReturnsAsync(new ToolExecutionResult { IsSuccessful = true });
        var agent = Agent(provider.Object, executor.Object, maxTurns);
        int polls = 0;
        agent.PendingInputProvider = _ => { polls++; return Task.FromResult<IReadOnlyList<IReadOnlyList<MessagePart>>>(Array.Empty<IReadOnlyList<MessagePart>>()); };
        await agent.ProcessMessageAsync("initial");
        Assert.Equal(0, polls);
    }
}
