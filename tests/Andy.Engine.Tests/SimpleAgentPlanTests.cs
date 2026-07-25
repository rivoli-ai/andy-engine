using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;
using ToolCall = Andy.Model.Model.ToolCall;

namespace Andy.Engine.Tests;

public class SimpleAgentPlanTests
{
    private static Mock<IToolRegistry> Registry(bool withReadFile = false)
    {
        var registry = new Mock<IToolRegistry>();
        var tools = new List<ToolRegistration>();
        if (withReadFile)
        {
            tools.Add(new ToolRegistration
            {
                IsEnabled = true,
                Metadata = new ToolMetadata
                {
                    Id = "read_file",
                    Name = "Read File",
                    Description = "Reads a file.",
                },
            });
        }
        registry.Setup(r => r.Tools).Returns(tools);
        registry.Setup(r => r.GetTool("read_file"))
            .Returns(tools.FirstOrDefault());
        return registry;
    }

    private static Mock<IToolExecutor> Executor(Action? onExecute = null)
    {
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<ToolExecutionContext>()))
            .Callback(onExecute ?? (() => { }))
            .ReturnsAsync(new ToolExecutionResult { IsSuccessful = true, Data = "ok" });
        return executor;
    }

    private static LlmResponse ToolResponse(params ToolCall[] calls) => new()
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = string.Empty,
            ToolCalls = calls.ToList(),
        },
    };

    private static LlmResponse FinalResponse(string content = "done") => new()
    {
        AssistantMessage = new Message { Role = Role.Assistant, Content = content },
        FinishReason = "stop",
    };

    private static ToolCall PlanCall(string id, string itemsJson) => new()
    {
        Id = id,
        Name = "update_plan",
        ArgumentsJson = $"{{\"items\":{itemsJson}}}",
    };

    [Fact]
    public async Task PlanningIsOptInAndDeclaresInternalToolWhenEnabled()
    {
        var disabledRequests = new List<LlmRequest>();
        var disabledProvider = new Mock<ILlmProvider>();
        disabledProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((request, _) => disabledRequests.Add(request))
            .ReturnsAsync(FinalResponse());
        var disabled = new SimpleAgent(
            disabledProvider.Object, Registry().Object, Executor().Object, "system");
        await disabled.ProcessMessageAsync("hello");
        disabledRequests.Single().Tools.Should().NotContain(tool => tool.Name == "update_plan");

        var enabledRequests = new List<LlmRequest>();
        var enabledProvider = new Mock<ILlmProvider>();
        enabledProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((request, _) => enabledRequests.Add(request))
            .ReturnsAsync(FinalResponse());
        var enabled = new SimpleAgent(
            enabledProvider.Object, Registry().Object, Executor().Object, "system",
            enablePlanning: true);
        await enabled.ProcessMessageAsync("hello");

        enabledRequests.Single().Tools.Should().ContainSingle(tool => tool.Name == "update_plan");
        enabled.CurrentPlan.Should().BeNull();
    }

    [Fact]
    public async Task PlanCreationEmitsSnapshotAndReachesNextModelContext()
    {
        var requests = new List<LlmRequest>();
        var provider = new Mock<ILlmProvider>();
        var responses = new Queue<LlmResponse>(new[]
        {
            ToolResponse(PlanCall(
                "p1",
                """
                [
                  {"id":"inspect","text":"Inspect repository","status":"in_progress"},
                  {"id":"test","text":"Run tests","status":"pending"}
                ]
                """)),
            FinalResponse(),
        });
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => responses.Dequeue());

        var events = new List<AgentPlanChangedEventArgs>();
        var agent = new SimpleAgent(
            provider.Object, Registry().Object, Executor().Object, "system",
            enablePlanning: true);
        agent.PlanChanged += (_, change) => events.Add(change);

        var result = await agent.ProcessMessageAsync("do multi-step work");

        result.Success.Should().BeTrue();
        events.Should().ContainSingle();
        events[0].Kind.Should().Be(AgentPlanChangeKind.Created);
        events[0].Plan.Items.Select(item => item.Id)
            .Should().ContainInOrder("inspect", "test");
        agent.CurrentPlan.Should().BeEquivalentTo(events[0].Plan);
        requests.Should().HaveCount(2);
        requests[1].SystemPrompt.Should().Contain("Current plan (revision 1)")
            .And.Contain("[>] inspect: Inspect repository")
            .And.Contain("[ ] test: Run tests");
    }

    [Fact]
    public void UpdatesPreserveIdentityOrderingAndRejectCompletedRegression()
    {
        var state = new AgentPlanState();
        var created = state.Apply(
            """{"items":[{"id":"a","text":"First","status":"in_progress"},{"id":"b","text":"Second","status":"pending"}]}""");
        var updated = state.Apply(
            """{"items":[{"id":"a","text":"First revised","status":"completed"},{"id":"b","text":"Second","status":"in_progress"},{"id":"c","text":"Third","status":"pending"}]}""");
        var rejected = state.Apply(
            """{"items":[{"id":"a","text":"First revised","status":"pending"},{"id":"b","text":"Second","status":"in_progress"}]}""");

        created.IsSuccessful.Should().BeTrue();
        updated.IsSuccessful.Should().BeTrue();
        updated.Plan!.Revision.Should().Be(2);
        updated.Plan.Items.Select(item => item.Id).Should().ContainInOrder("a", "b", "c");
        updated.Plan.Items[0].Text.Should().Be("First revised");
        rejected.IsSuccessful.Should().BeFalse();
        state.Current.Should().BeSameAs(updated.Plan);
    }

    [Fact]
    public async Task PlanEventPrecedesDependentExternalToolAndResultsKeepCallOrder()
    {
        var eventSeen = false;
        var requests = new List<LlmRequest>();
        var provider = new Mock<ILlmProvider>();
        var responses = new Queue<LlmResponse>(new[]
        {
            ToolResponse(
                PlanCall(
                    "plan-call",
                    """[{"id":"read","text":"Read file","status":"in_progress"}]"""),
                new ToolCall
                {
                    Id = "read-call",
                    Name = "read_file",
                    ArgumentsJson = """{"path":"README.md"}""",
                }),
            FinalResponse(),
        });
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => responses.Dequeue());
        var agent = new SimpleAgent(
            provider.Object,
            Registry(withReadFile: true).Object,
            Executor(() => eventSeen.Should().BeTrue()).Object,
            "system",
            enablePlanning: true);
        agent.PlanChanged += (_, _) => eventSeen = true;

        await agent.ProcessMessageAsync("read it");

        var toolResults = requests[1].Messages
            .Where(message => message.Role == Role.Tool)
            .SelectMany(message => message.ToolResults ?? new List<Andy.Model.Model.ToolResult>())
            .Select(result => result.CallId);
        toolResults.Should().ContainInOrder("plan-call", "read-call");
    }

    [Fact]
    public async Task ThrowingPlanSubscriberDoesNotFaultRunOrBlockOtherSubscribers()
    {
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResponse(PlanCall(
                "p1",
                """[{"id":"a","text":"Do work","status":"in_progress"}]""")))
            .ReturnsAsync(FinalResponse());
        var observed = false;
        var agent = new SimpleAgent(
            provider.Object, Registry().Object, Executor().Object, "system",
            enablePlanning: true);
        agent.PlanChanged += (_, _) => throw new InvalidOperationException("subscriber");
        agent.PlanChanged += (_, _) => observed = true;

        var result = await agent.ProcessMessageAsync("work");

        result.Success.Should().BeTrue();
        observed.Should().BeTrue();
    }

    [Fact]
    public async Task PlanRoundTripsWithTranscript()
    {
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResponse(PlanCall(
                "p1",
                """[{"id":"a","text":"Do work","status":"completed"}]""")))
            .ReturnsAsync(FinalResponse());
        var original = new SimpleAgent(
            provider.Object, Registry().Object, Executor().Object, "system",
            enablePlanning: true);
        await original.ProcessMessageAsync("work");

        var snapshot = TranscriptSnapshot.FromJson(original.ExportTranscript().ToJson());
        var restored = new SimpleAgent(
            Mock.Of<ILlmProvider>(), Registry().Object, Executor().Object, "system",
            enablePlanning: true);
        restored.RestoreTranscript(snapshot);

        restored.CurrentPlan.Should().BeEquivalentTo(original.CurrentPlan);
        restored.ExportTranscript().ToJson().Should().Be(original.ExportTranscript().ToJson());
    }
}
