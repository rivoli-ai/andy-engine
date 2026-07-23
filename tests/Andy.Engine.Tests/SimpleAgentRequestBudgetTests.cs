using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;
using ToolCall = Andy.Model.Model.ToolCall;
using ToolResult = Andy.Model.Model.ToolResult;

namespace Andy.Engine.Tests;

/// <summary>
/// The context token budget must hold INSIDE the turn loop, not just at loop entry: a long tool
/// loop accumulates up to _maxToolResultChars per call in in-flight messages, which previously
/// grew the request unbounded until the provider rejected it mid-run (issue #36). Related: the
/// old compression path could not shrink tool results at all because providers serialize tool
/// messages from ToolResult.ResultJson, which passed through untouched (issue #38).
/// </summary>
public class SimpleAgentRequestBudgetTests
{
    private static readonly Mock<IToolRegistry> EmptyRegistry = CreateRegistry();

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

    private static Message ToolMessage(string callId, string payload) => new()
    {
        Role = Role.Tool,
        Content = payload,
        ToolResults = new List<ToolResult>
        {
            new() { CallId = callId, Name = "read_file", ResultJson = payload },
        },
    };

    private static Message AssistantToolCall(string callId) => new()
    {
        Role = Role.Assistant,
        Content = "",
        ToolCalls = new List<ToolCall>
        {
            new() { Id = callId, Name = "read_file", ArgumentsJson = "{\"path\":\"x\"}" },
        },
    };

    private static SimpleAgent CreateAgent(ILlmProvider provider, IToolExecutor executor, int maxContextTokens) =>
        new(provider, EmptyRegistry.Object, executor, "system", maxTurns: 60,
            maxContextTokens: maxContextTokens);

    [Fact]
    public void FitRequestToBudget_UnderBudget_PassesThroughUnchanged()
    {
        var agent = CreateAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolExecutor>(), maxContextTokens: 10_000);
        var context = new List<Message> { new() { Role = Role.User, Content = "hi" } };
        var inFlight = new List<Message> { new() { Role = Role.User, Content = "now" } };

        var fitted = agent.FitRequestToBudget(context, inFlight);

        fitted.Should().HaveCount(2);
        fitted[0].Should().BeSameAs(context[0]);
        fitted[1].Should().BeSameAs(inFlight[0]);
    }

    [Fact]
    public void FitRequestToBudget_ElidesOldToolResults_WithoutMutatingOriginals()
    {
        // Budget of 2000 tokens ≈ 8000 chars; three 16k-char tool results blow it.
        var agent = CreateAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolExecutor>(), maxContextTokens: 2_000);
        var big = new string('x', 16_000);
        var inFlight = new List<Message> { new() { Role = Role.User, Content = "task" } };
        for (var i = 0; i < 3; i++)
        {
            inFlight.Add(AssistantToolCall($"c{i}"));
            inFlight.Add(ToolMessage($"c{i}", big));
        }
        // Recent tail beyond the protected window so the oldest results are eligible.
        for (var i = 0; i < 8; i++)
            inFlight.Add(new Message { Role = Role.User, Content = $"nudge {i}" });

        var fitted = agent.FitRequestToBudget(new List<Message>(), inFlight);

        // The oldest tool result was elided in the REQUEST VIEW...
        var firstToolMsg = fitted.First(m => m.Role == Role.Tool);
        firstToolMsg.ToolResults![0].ResultJson.Should().Contain("\"elided\":true");
        firstToolMsg.ToolResults[0].CallId.Should().Be("c0"); // pairing intact
        firstToolMsg.Content.Should().Contain("elided");
        // ...while the in-flight (committed-to-history) message keeps the full payload.
        inFlight.First(m => m.Role == Role.Tool).ToolResults![0].ResultJson.Should().Be(big);

        SimpleAgentRequestBudgetTestsHelpers.TotalChars(fitted)
            .Should().BeLessThanOrEqualTo(2_000L * 4 + 16_000,
                "at most the protected tail can stay oversized in this layout");
    }

    [Fact]
    public void FitRequestToBudget_ProtectedTail_IsNeverElided()
    {
        var agent = CreateAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolExecutor>(), maxContextTokens: 1_000);
        var big = new string('y', 20_000);
        var inFlight = new List<Message>
        {
            new() { Role = Role.User, Content = "task" },
            AssistantToolCall("recent"),
            ToolMessage("recent", big),
        };

        var fitted = agent.FitRequestToBudget(new List<Message>(), inFlight);

        // The only tool result is within the protected tail: it must arrive intact even though
        // the request is over budget.
        fitted.First(m => m.Role == Role.Tool).ToolResults![0].ResultJson.Should().Be(big);
    }

    [Fact]
    public void FitRequestToBudget_DropsOldestCommittedTurns_NeverInFlight()
    {
        var agent = CreateAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolExecutor>(), maxContextTokens: 1_000);
        // Committed history: prose-only turns too large for the budget even after tool elision
        // (no tool results to elide).
        var context = new List<Message>();
        for (var i = 0; i < 10; i++)
        {
            context.Add(new Message { Role = Role.User, Content = $"old question {i} " + new string('p', 1_000) });
            context.Add(new Message { Role = Role.Assistant, Content = $"old answer {i} " + new string('q', 1_000) });
        }
        var inFlight = new List<Message> { new() { Role = Role.User, Content = "current task" } };

        var fitted = agent.FitRequestToBudget(context, inFlight);

        fitted.Should().Contain(m => m.Content == "current task", "in-flight messages are never dropped");
        fitted.Count.Should().BeLessThan(context.Count + inFlight.Count, "oldest committed turns are dropped");
        // Turns drop whole (user+assistant together), oldest first.
        if (fitted.Count > 1)
            fitted[0].Role.Should().Be(Role.User);
        SimpleAgentRequestBudgetTestsHelpers.TotalChars(fitted).Should().BeLessThanOrEqualTo(1_000L * 4 + 2_100,
            "at most one turn of slack remains");
    }

    [Fact]
    public async Task LongToolLoop_RequestSizePlateaus_HistoryKeepsFullResults()
    {
        // ~1500-token budget (≈6000 chars); each tool round returns ~4000 chars, 12 rounds ≈ 48k+.
        const int budget = 1_500;
        const int toolRounds = 12;
        var registry = CreateRegistry();
        var bigResult = new string('z', 4_000);

        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = bigResult });

        var capturedRequests = new List<LlmRequest>();
        var provider = new Mock<ILlmProvider>();
        var call = 0;
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((r, _) => capturedRequests.Add(r))
            .ReturnsAsync(() =>
            {
                call++;
                if (call <= toolRounds)
                {
                    return new LlmResponse
                    {
                        AssistantMessage = new Message
                        {
                            Role = Role.Assistant,
                            Content = "",
                            ToolCalls = new List<ToolCall>
                            {
                                new() { Id = $"call_{call}", Name = "read_file", ArgumentsJson = "{}" },
                            },
                        },
                    };
                }

                return new LlmResponse
                {
                    AssistantMessage = new Message { Role = Role.Assistant, Content = "done" },
                    FinishReason = "stop",
                };
            });

        var agent = new SimpleAgent(provider.Object, registry.Object, executor.Object, "system",
            maxTurns: 20, maxContextTokens: budget);

        var result = await agent.ProcessMessageAsync("do the long thing");

        result.Success.Should().BeTrue();
        capturedRequests.Should().HaveCount(toolRounds + 1);

        // The final request must have its OLD tool results elided while the most recent ones
        // (the protected tail) arrive full-size.
        var lastRequest = capturedRequests[^1].Messages;
        var lastToolMessages = lastRequest.Where(m => m.Role == Role.Tool).ToList();
        lastToolMessages.Should().HaveCount(toolRounds);
        lastToolMessages.Count(m => m.ToolResults![0].ResultJson.Contains("\"elided\":true"))
            .Should().BeGreaterThanOrEqualTo(toolRounds - 4, "only the protected tail may stay full-size");
        lastToolMessages[^1].ToolResults![0].ResultJson.Should().Contain(bigResult,
            "the most recent tool result must reach the model verbatim");

        // Growth must PLATEAU: once elision reaches steady state, each extra round adds only an
        // elided stub (~600 chars) instead of a full ~8k tool round. Without in-loop enforcement
        // these four rounds would add ~32k chars.
        var growth = SimpleAgentRequestBudgetTestsHelpers.TotalChars(capturedRequests[^1].Messages)
                   - SimpleAgentRequestBudgetTestsHelpers.TotalChars(capturedRequests[^5].Messages);
        growth.Should().BeLessThan(4 * 2_500);

        // The committed conversation retains the FULL tool results — only the request view shrinks.
        var toolMessages = agent.Conversation.Turns.Single().ToolMessages!
            .Where(m => m.Role == Role.Tool).ToList();
        toolMessages.Should().HaveCount(toolRounds);
        toolMessages.Should().OnlyContain(m => m.ToolResults![0].ResultJson.Contains(bigResult));
    }
}

internal static class SimpleAgentRequestBudgetTestsHelpers
{
    public static long TotalChars(IEnumerable<Message> messages) =>
        messages.Sum(SimpleAgent.EstimateMessageChars);
}
