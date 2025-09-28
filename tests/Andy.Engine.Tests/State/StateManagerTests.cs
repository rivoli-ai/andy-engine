using Andy.Engine.Contracts;
using Andy.Engine.Planner;
using Andy.Engine.State;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Engine.Tests.State;

public class StateManagerTests
{
    private readonly StateManager _sut;
    private readonly Mock<IStateStore> _mockStateStore;
    private readonly Mock<ILogger<StateManager>> _mockLogger;

    public StateManagerTests()
    {
        _mockStateStore = new Mock<IStateStore>();
        _mockLogger = new Mock<ILogger<StateManager>>();
        _sut = new StateManager(
            _mockStateStore.Object,
            StateOptions.Default,
            _mockLogger.Object
        );
    }

    [Fact]
    public void CreateInitialState_ShouldReturnValidState()
    {
        // Arrange
        var goal = new AgentGoal(
            UserGoal: "Test goal",
            Constraints: new[] { "Constraint 1" }
        );
        var budget = new Budget(
            MaxTurns: 10,
            MaxWallClock: TimeSpan.FromMinutes(5)
        );

        // Act
        var state = _sut.CreateInitialState(goal, budget);

        // Assert
        state.Should().NotBeNull();
        state.Goal.Should().Be(goal);
        state.Budget.Should().Be(budget);
        state.TurnIndex.Should().Be(0);
        state.Subgoals.Should().BeEmpty();
        state.LastAction.Should().BeNull();
        state.LastObservation.Should().BeNull();
        state.WorkingMemoryDigest.Should().BeEmpty();
    }

    [Fact]
    public void UpdateState_WithToolDecision_ShouldUpdateLastAction()
    {
        // Arrange
        var state = CreateTestState();
        var toolCall = new ToolCall(
            ToolName: "test_tool",
            Args: new System.Text.Json.Nodes.JsonObject()
        );
        var decision = new CallToolDecision(toolCall);

        // Act
        var newState = _sut.UpdateState(state, decision, null, null);

        // Assert
        newState.LastAction.Should().Be(toolCall);
        newState.TurnIndex.Should().Be(state.TurnIndex + 1);
    }

    [Fact]
    public void UpdateState_WithReplanDecision_ShouldUpdateSubgoals()
    {
        // Arrange
        var state = CreateTestState();
        var newSubgoals = new List<string> { "Subgoal 1", "Subgoal 2" };
        var decision = new ReplanDecision(newSubgoals);

        // Act
        var newState = _sut.UpdateState(state, decision, null, null);

        // Assert
        newState.Subgoals.Should().BeEquivalentTo(newSubgoals);
        newState.WorkingMemoryDigest.Should().ContainKey("replan");
    }

    [Fact]
    public void UpdateState_WithObservation_ShouldAddKeyFacts()
    {
        // Arrange
        var state = CreateTestState();
        var observation = new Observation(
            Summary: "Test observation",
            KeyFacts: new Dictionary<string, string>
            {
                ["fact1"] = "value1",
                ["fact2"] = "value2"
            },
            Affordances: new List<string> { "action1" },
            Raw: new ToolResult(Ok: true)
        );
        var decision = new CallToolDecision(new ToolCall("test", new System.Text.Json.Nodes.JsonObject()));

        // Act
        var newState = _sut.UpdateState(state, decision, observation, null);

        // Assert
        newState.LastObservation.Should().Be(observation);
        newState.WorkingMemoryDigest.Should().ContainKey("fact_fact1");
        newState.WorkingMemoryDigest["fact_fact1"].Should().Be("value1");
    }

    [Fact]
    public async Task SaveStateAsync_ShouldPersistState()
    {
        // Arrange
        var traceId = Guid.NewGuid();
        var state = CreateTestState();

        // Act
        await _sut.SaveStateAsync(traceId, state);

        // Assert
        _mockStateStore.Verify(
            x => x.SaveAsync(traceId, state, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task LoadStateAsync_WhenNotCached_ShouldLoadFromStore()
    {
        // Arrange
        var traceId = Guid.NewGuid();
        var expectedState = CreateTestState();
        _mockStateStore.Setup(x => x.LoadAsync(traceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedState);

        // Act
        var state = await _sut.LoadStateAsync(traceId);

        // Assert
        state.Should().Be(expectedState);
        _mockStateStore.Verify(
            x => x.LoadAsync(traceId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task LoadStateAsync_WhenCached_ShouldReturnCachedValue()
    {
        // Arrange
        var traceId = Guid.NewGuid();
        var state = CreateTestState();
        await _sut.SaveStateAsync(traceId, state);

        // Act
        var loadedState = await _sut.LoadStateAsync(traceId);

        // Assert
        loadedState.Should().Be(state);
        _mockStateStore.Verify(
            x => x.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ClearStateAsync_ShouldRemoveFromCacheAndStore()
    {
        // Arrange
        var traceId = Guid.NewGuid();
        var state = CreateTestState();
        await _sut.SaveStateAsync(traceId, state);

        // Act
        await _sut.ClearStateAsync(traceId);

        // Assert
        _mockStateStore.Verify(
            x => x.DeleteAsync(traceId, It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Cache should be cleared
        var loadedState = await _sut.LoadStateAsync(traceId);
        loadedState.Should().BeNull();
    }

    private static AgentState CreateTestState()
    {
        return new AgentState(
            Goal: new AgentGoal("Test goal", new[] { "Constraint" }),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: null,
            Budget: new Budget(10, TimeSpan.FromMinutes(5)),
            TurnIndex: 0,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );
    }
}