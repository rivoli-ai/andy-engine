using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// Structured multimodal user messages (issue #35): text + image parts must reach a
/// vision-capable provider as structured content, be rejected explicitly by text-only paths
/// (never silently flattened), survive tool rounds, history, the compressed request view, and
/// transcript snapshot/restore, with bounded media metadata validated at the API boundary.
/// </summary>
public class SimpleAgentMultimodalTests
{
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

    /// <summary>
    /// Fake vision-capable provider: records every request and replays scripted responses.
    /// </summary>
    private sealed class VisionProvider : IVisionCapableLlmProvider
    {
        private readonly Queue<LlmResponse> _responses;
        public List<LlmRequest> Requests { get; } = new();
        public bool AcceptsImages { get; set; } = true;

        public VisionProvider(params LlmResponse[] responses) => _responses = new Queue<LlmResponse>(responses);

        public string Name => "fake-vision";

        public ValueTask<bool> SupportsImageInputAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(AcceptsImages);

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }

        public IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IEnumerable<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<ModelInfo>());
    }

    private static LlmResponse FinalAnswer(string text = "done") => new()
    {
        AssistantMessage = new Message { Role = Role.Assistant, Content = text },
        FinishReason = "stop",
    };

    private static LlmResponse ToolCallRound(string callId, string toolName) => new()
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = "",
            ToolCalls = new List<ToolCall> { new() { Id = callId, Name = toolName, ArgumentsJson = "{}" } },
        },
    };

    private static SimpleAgent NewAgent(ILlmProvider provider, IToolExecutor? executor = null, int maxImageBytes = 0)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new List<ToolRegistration>());
        return new SimpleAgent(
            provider,
            registry.Object,
            executor ?? Mock.Of<IToolExecutor>(),
            systemPrompt: "system",
            maxTurns: 10,
            maxImageBytes: maxImageBytes);
    }

    private static List<MessagePart> TextPlusImage() => new()
    {
        new TextPart("what is in this picture?"),
        new ImagePart { MimeType = "image/png", ImageData = PngBytes },
    };

    // ---- structured content reaches a vision-capable provider ----

    [Fact]
    public async Task TextPlusImage_ReachesVisionProvider_AsStructuredParts()
    {
        var provider = new VisionProvider(FinalAnswer());
        var agent = NewAgent(provider);

        var result = await agent.ProcessMessageAsync(TextPlusImage());

        Assert.True(result.Success);
        var userMsg = Assert.Single(provider.Requests).Messages.Single(m => m.Role == Role.User);

        // Content carries the text for part-unaware consumers; the ordered structured parts are
        // attached and NOT flattened into the string.
        Assert.Equal("what is in this picture?", userMsg.Content);
        var parts = MultimodalMessage.GetAttachedParts(userMsg);
        Assert.NotNull(parts);
        Assert.Equal(2, parts!.Count);
        Assert.Equal("what is in this picture?", Assert.IsType<TextPart>(parts[0]).Text);
        var image = Assert.IsType<ImagePart>(parts[1]);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(PngBytes, image.ImageData);
    }

    [Fact]
    public async Task TextOnlyParts_WorkWithout_VisionCapableProvider()
    {
        var provider = new Mock<ILlmProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FinalAnswer("hi"));
        var agent = NewAgent(provider.Object);

        var result = await agent.ProcessMessageAsync(
            new List<MessagePart> { new TextPart("hello") });

        Assert.True(result.Success);
        Assert.Equal("hi", result.Response);
    }

    // ---- capability gate: text-only paths reject explicitly, never silently discard ----

    [Fact]
    public async Task NonVisionProvider_RejectsImageMessage_BeforeDispatch()
    {
        var provider = new Mock<ILlmProvider>(MockBehavior.Strict);
        provider.SetupGet(p => p.Name).Returns("text-only");
        var agent = NewAgent(provider.Object);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => agent.ProcessMessageAsync(TextPlusImage()));

        Assert.Contains("image", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Strict mock: any CompleteAsync/StreamCompleteAsync call would have thrown — nothing
        // was dispatched, so the image cannot have been silently dropped on the wire.
        provider.Verify(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(agent.GetHistory());
    }

    [Fact]
    public async Task VisionProvider_ReportingNoImageSupport_Rejects()
    {
        var provider = new VisionProvider(FinalAnswer()) { AcceptsImages = false };
        var agent = NewAgent(provider);

        await Assert.ThrowsAsync<NotSupportedException>(() => agent.ProcessMessageAsync(TextPlusImage()));
        Assert.Empty(provider.Requests);
    }

    // ---- bounded media metadata ----

    public static TheoryData<string, MessagePart> MalformedImageParts() => new()
    {
        { "no media type", new ImagePart { ImageData = new byte[] { 1 } } },
        { "non-image media type", new ImagePart { MimeType = "application/pdf", ImageData = new byte[] { 1 } } },
        { "no source", new ImagePart { MimeType = "image/png" } },
        {
            "both sources",
            new ImagePart { MimeType = "image/png", ImageData = new byte[] { 1 }, ImageUrl = "https://example.com/a.png" }
        },
        { "relative uri", new ImagePart { MimeType = "image/png", ImageUrl = "images/a.png" } },
        { "unsupported scheme", new ImagePart { MimeType = "image/png", ImageUrl = "ftp://example.com/a.png" } },
    };

    [Theory]
    [MemberData(nameof(MalformedImageParts))]
    public async Task MalformedImagePart_IsRejected(string label, MessagePart part)
    {
        var provider = new VisionProvider(FinalAnswer());
        var agent = NewAgent(provider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => agent.ProcessMessageAsync(new List<MessagePart> { new TextPart("t"), part }));
        Assert.Empty(provider.Requests);
        Assert.NotNull(label);
    }

    [Fact]
    public async Task EmptyPartList_And_UnsupportedPartTypes_AreRejected()
    {
        var agent = NewAgent(new VisionProvider(FinalAnswer()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => agent.ProcessMessageAsync(new List<MessagePart>()));
        await Assert.ThrowsAsync<ArgumentException>(
            () => agent.ProcessMessageAsync(new List<MessagePart> { new TextPart("   ") }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => agent.ProcessMessageAsync(new List<MessagePart> { new ToolCallPart(new ToolCall()) }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => agent.ProcessMessageAsync(new List<MessagePart> { new TextPart("t"), null! }));
    }

    [Fact]
    public async Task ImageSizeBound_IsEnforced_AtTheCap()
    {
        var provider = new VisionProvider(FinalAnswer(), FinalAnswer());
        var agent = NewAgent(provider, maxImageBytes: 8);

        // Exactly at the cap: accepted.
        var atCap = await agent.ProcessMessageAsync(new List<MessagePart>
        {
            new ImagePart { MimeType = "image/png", ImageData = new byte[8] },
        });
        Assert.True(atCap.Success);

        // One byte over: rejected with the sizes in the message, before dispatch.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => agent.ProcessMessageAsync(new List<MessagePart>
        {
            new ImagePart { MimeType = "image/png", ImageData = new byte[9] },
        }));
        Assert.Contains("9", ex.Message);
        Assert.Contains("8", ex.Message);
        Assert.Single(provider.Requests);
    }

    // ---- tool-loop continuation ----

    [Fact]
    public async Task ToolRounds_RetainTheOriginalMultimodalUserTurn_OnEveryRequest()
    {
        var provider = new VisionProvider(ToolCallRound("call_1", "lookup"), FinalAnswer());
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok", Message = "ok" });
        var agent = NewAgent(provider, executor.Object);

        var result = await agent.ProcessMessageAsync(TextPlusImage());

        Assert.True(result.Success);
        Assert.Equal(2, provider.Requests.Count);
        foreach (var request in provider.Requests)
        {
            var userMsg = request.Messages.Single(m => m.Role == Role.User);
            var parts = MultimodalMessage.GetAttachedParts(userMsg);
            Assert.NotNull(parts);
            var image = Assert.Single(parts!.OfType<ImagePart>());
            Assert.Equal(PngBytes, image.ImageData);
        }
    }

    // ---- history and the compressed request view ----

    [Fact]
    public async Task NextCall_RequestView_StillCarriesThePriorTurnsImage()
    {
        var provider = new VisionProvider(FinalAnswer(), FinalAnswer());
        var agent = NewAgent(provider);

        await agent.ProcessMessageAsync(TextPlusImage());
        // Second, plain-text call: its request context is rebuilt through the Andy.Context
        // compression round-trip, which must not drop the prior turn's structured parts.
        await agent.ProcessMessageAsync("and now?");

        var secondRequest = provider.Requests[1];
        var multimodalUser = secondRequest.Messages.First(m => m.Role == Role.User);
        var parts = MultimodalMessage.GetAttachedParts(multimodalUser);
        Assert.NotNull(parts);
        Assert.Equal(PngBytes, Assert.Single(parts!.OfType<ImagePart>()).ImageData);
    }

    // ---- transcript snapshot / restore ----

    [Fact]
    public async Task ImageContent_Survives_SnapshotJsonRoundTrip_AndRestore()
    {
        var provider = new VisionProvider(FinalAnswer());
        var agent = NewAgent(provider);
        await agent.ProcessMessageAsync(new List<MessagePart>
        {
            new TextPart("look:"),
            new ImagePart { MimeType = "image/jpeg", ImageData = PngBytes },
            new ImagePart { MimeType = "image/png", ImageUrl = "https://example.com/pic.png" },
        });

        var json = agent.ExportTranscript().ToJson();
        var restoredAgent = NewAgent(new VisionProvider(FinalAnswer()));
        restoredAgent.RestoreTranscript(TranscriptSnapshot.FromJson(json));

        var userMsg = restoredAgent.GetHistory().Single(m => m.Role == Role.User);
        var parts = MultimodalMessage.GetAttachedParts(userMsg);
        Assert.NotNull(parts);
        Assert.Equal(3, parts!.Count);
        Assert.Equal("look:", Assert.IsType<TextPart>(parts[0]).Text);
        var bytesImage = Assert.IsType<ImagePart>(parts[1]);
        Assert.Equal("image/jpeg", bytesImage.MimeType);
        Assert.Equal(PngBytes, bytesImage.ImageData);
        var urlImage = Assert.IsType<ImagePart>(parts[2]);
        Assert.Equal("https://example.com/pic.png", urlImage.ImageUrl);
        Assert.Null(urlImage.ImageData);

        // Re-export is stable: the restored transcript serializes to the same JSON.
        Assert.Equal(json, restoredAgent.ExportTranscript().ToJson());
    }

    [Fact]
    public void Snapshot_WithMalformedImagePart_FailsRestore_Deterministically()
    {
        var snapshot = new TranscriptSnapshot
        {
            Turns = new[]
            {
                new TranscriptTurn
                {
                    User = new TranscriptMessage
                    {
                        Role = "user",
                        Content = "hi",
                        Parts = new[] { new TranscriptPart { Type = "image", MimeType = "image/png" } },
                    },
                    FinalAssistant = new TranscriptMessage { Role = "assistant", Content = "ok" },
                },
            },
        };
        var agent = NewAgent(new VisionProvider());

        Assert.Throws<ArgumentException>(() => agent.RestoreTranscript(snapshot));
        Assert.Empty(agent.GetHistory());

        var badBase64 = snapshot with
        {
            Turns = new[]
            {
                new TranscriptTurn
                {
                    User = new TranscriptMessage
                    {
                        Role = "user",
                        Content = "hi",
                        Parts = new[]
                        {
                            new TranscriptPart { Type = "image", MimeType = "image/png", ImageDataBase64 = "@@not-base64@@" },
                        },
                    },
                },
            },
        };
        Assert.Throws<ArgumentException>(() => agent.RestoreTranscript(badBase64));
        Assert.Empty(agent.GetHistory());
    }

    [Fact]
    public void PlainTextSnapshots_SerializeWithoutAPartsField()
    {
        var snapshot = new TranscriptSnapshot
        {
            Turns = new[]
            {
                new TranscriptTurn
                {
                    User = new TranscriptMessage { Role = "user", Content = "hi" },
                    FinalAssistant = new TranscriptMessage { Role = "assistant", Content = "ok" },
                },
            },
        };

        Assert.DoesNotContain("\"parts\"", snapshot.ToJson());
    }

    // ---- cancellation ----

    [Fact]
    public async Task Cancellation_DuringMultimodalCall_Propagates_AndCommitsTheTurnWithParts()
    {
        using var cts = new CancellationTokenSource();
        var provider = new VisionProvider(); // no scripted responses; call must cancel first
        cts.Cancel();
        var agent = NewAgent(provider);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.ProcessMessageAsync(TextPlusImage(), cts.Token));

        // The interrupted turn is committed (issue #37) and its user message keeps the parts.
        var userMsg = agent.GetHistory().Single(m => m.Role == Role.User);
        var parts = MultimodalMessage.GetAttachedParts(userMsg);
        Assert.NotNull(parts);
        Assert.Single(parts!.OfType<ImagePart>());
    }
}
