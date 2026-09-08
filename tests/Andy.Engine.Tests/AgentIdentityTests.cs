using Andy.Model.Llm;
using Andy.Tools.Core;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

public class AgentIdentityTests
{
    private static AgentIdentityState State(int? pid = 10, string? os = "source") => new(runtime: () => new()
    {
        InstanceId = "template",
        ProcessId = pid,
        OperatingSystem = os
    });

    [Fact]
    public async Task ToolCallNamesTheCallingAgentAndExportsItsHistory()
    {
        var tool = new Andy.Tools.Library.System.SetAgentNameTool();
        await tool.InitializeAsync();
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.Tools).Returns(new[] { new ToolRegistration { IsEnabled = true, Metadata = tool.Metadata } });
        var executor = new Mock<IToolExecutor>();
        executor.Setup(e => e.ExecuteAsync("set_agent_name", It.IsAny<Dictionary<string, object?>>(), It.IsAny<ToolExecutionContext>()))
            .Returns(async (string _, Dictionary<string, object?> args, ToolExecutionContext context) =>
            {
                var result = await tool.ExecuteAsync(args, context);
                Assert.True(result.IsSuccessful, result.ErrorMessage);
                return new ToolExecutionResult { IsSuccessful = result.IsSuccessful, Data = result.Data };
            });
        var provider = new Mock<ILlmProvider>();
        provider.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                AssistantMessage = new Andy.Model.Model.Message
                {
                    Role = Andy.Model.Model.Role.Assistant,
                    ToolCalls = new List<Andy.Model.Model.ToolCall> { new() { Id = "name1", Name = "set_agent_name", ArgumentsJson = "{\"name\":\"cedar\"}" } }
                }
            })
            .ReturnsAsync(new LlmResponse { AssistantMessage = new Andy.Model.Model.Message { Role = Andy.Model.Model.Role.Assistant, Content = "Named." }, FinishReason = "stop" });
        using var agent = new SimpleAgent(provider.Object, registry.Object, executor.Object, "system");
        var response = await agent.ProcessMessageAsync("Name yourself cedar");
        Assert.True(response.Success);
        Assert.Equal("cedar", agent.ExportTranscript().Identity!.Name);
        Assert.Equal("renamed", agent.Identity.GetSnapshot().History[^1].Kind);
        executor.Verify(e => e.ExecuteAsync("set_agent_name", It.IsAny<Dictionary<string, object?>>(), It.Is<ToolExecutionContext>(c => c.AgentIdentity == agent.Identity)), Times.Once);
    }

    [Fact]
    public void RenameClearAndNoOpRetainStableIdentityAndHistory()
    {
        var identity = State();
        var initial = identity.GetSnapshot();
        identity.SetName(" cedar ");
        identity.SetName("cedar");
        var renamed = identity.SetName("oak");
        var cleared = identity.SetName("");
        Assert.Equal(initial.AgentId, cleared.AgentId);
        Assert.Equal(initial.Activation, cleared.Activation);
        Assert.Null(cleared.Name);
        Assert.Equal(new[] { "created", "renamed", "renamed", "cleared" }, cleared.History.Select(e => e.Kind));
        Assert.Equal("cedar", renamed.History[^1].PreviousName);
        Assert.Equal("oak", cleared.History[^1].PreviousName);
        Assert.Single(initial.History);
    }

    [Fact]
    public void PortableSnapshotRetainsHistoryButRefreshesRuntimeOnEveryResume()
    {
        var source = State();
        source.SetName("cedar");
        var json = new TranscriptSnapshot { Identity = source.GetSnapshot() }.ToJson();
        var imported = TranscriptSnapshot.FromJson(json).Identity!;
        var target = State(99, "destination");
        target.Restore(imported);
        var restored = target.GetSnapshot();
        Assert.Equal(imported.AgentId, restored.AgentId);
        Assert.Equal("cedar", restored.Name);
        Assert.NotEqual(imported.Activation.InstanceId, restored.Activation.InstanceId);
        Assert.Equal(99, restored.Activation.ProcessId);
        Assert.Equal("destination", restored.Activation.OperatingSystem);
        Assert.Equal(imported.History, restored.History.Take(imported.History.Count));
        Assert.Equal("resumed", restored.History[^1].Kind);
        var concurrent = State(99, "destination");
        concurrent.Restore(imported);
        Assert.NotEqual(restored.Activation.InstanceId, concurrent.GetSnapshot().Activation.InstanceId);
    }

    [Fact]
    public void MissingRuntimeFieldsAndConcurrentChangesAreSupported()
    {
        var state = State(null, null);
        Parallel.For(0, 100, i => state.SetName($"worker-{i}"));
        var snapshot = state.GetSnapshot();
        Assert.Equal(101, snapshot.History.Count);
        Assert.Equal(Enumerable.Range(1, 101).Select(i => (long)i), snapshot.History.Select(e => e.Sequence));
        Assert.Null(snapshot.Activation.ProcessId);
        Assert.Null(State().GetSnapshot().Name);
    }

    [Fact]
    public void InvalidNameOrSnapshotLeavesStateUnchanged()
    {
        var state = State();
        var before = state.GetSnapshot();
        Assert.Throws<ArgumentException>(() => state.SetName("bad\nname"));
        Assert.Throws<ArgumentException>(() => state.Restore(before with { Version = 99 }));
        Assert.Equal(before.AgentId, state.GetSnapshot().AgentId);
        Assert.Equal(before.History, state.GetSnapshot().History);
    }

    [Fact]
    public void SimpleAgentExportsRestoresAndRetainsIdentityWhenConversationIsCleared()
    {
        using var source = new SimpleAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolRegistry>(), Mock.Of<IToolExecutor>(), "system");
        source.Identity.SetName("cedar");
        source.ClearHistory();
        var saved = source.ExportTranscript();
        using var restored = new SimpleAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolRegistry>(), Mock.Of<IToolExecutor>(), "system");
        restored.RestoreTranscript(TranscriptSnapshot.FromJson(saved.ToJson()));
        Assert.Equal("cedar", restored.Identity.GetSnapshot().Name);
        Assert.Equal(saved.Identity!.AgentId, restored.Identity.GetSnapshot().AgentId);
        Assert.Equal("resumed", restored.Identity.GetSnapshot().History[^1].Kind);
        using var legacy = new SimpleAgent(Mock.Of<ILlmProvider>(), Mock.Of<IToolRegistry>(), Mock.Of<IToolExecutor>(), "system");
        legacy.RestoreTranscript(new TranscriptSnapshot());
        Assert.Null(legacy.Identity.GetSnapshot().Name);
        Assert.Single(legacy.Identity.GetSnapshot().History);
    }
}
