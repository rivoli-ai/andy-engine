using Andy.Engine.Contracts;
using Andy.Engine.Planner;
using Andy.Engine.Policy;
using FluentAssertions;
using System.Text.Json.Nodes;
using Xunit;

namespace Andy.Engine.Tests.Policy;

public class PolicyEngineTests
{
    private readonly PolicyEngine _sut;

    public PolicyEngineTests()
    {
        _sut = new PolicyEngine();
    }

    [Fact]
    public void Resolve_CallToolDecision_ShouldReturnCallToolAction()
    {
        // Arrange
        var toolCall = new ToolCall("test_tool", JsonNode.Parse("""{"param": "value"}"""));
        var decision = new CallToolDecision(toolCall);
        var policy = new ErrorHandlingPolicy(
            MaxRetries: 3,
            BaseBackoff: TimeSpan.FromSeconds(1),
            UseFallbacks: false,
            AskUserWhenMissingFields: false
        );
        var state = CreateTestState();

        // Act
        var action = _sut.Resolve(decision, null, policy, state);

        // Assert
        action.Should().BeOfType<CallToolAction>();
        var callToolAction = action as CallToolAction;
        callToolAction!.Call.Should().Be(toolCall);
    }

    [Fact]
    public void Resolve_StopDecision_ShouldReturnStopAction()
    {
        // Arrange
        var decision = new StopDecision("Task completed");
        var policy = CreateDefaultPolicy();
        var state = CreateTestState();

        // Act
        var action = _sut.Resolve(decision, null, policy, state);

        // Assert
        action.Should().BeOfType<StopAction>();
        var stopAction = action as StopAction;
        stopAction!.Reason.Should().Be("Task completed");
    }

    [Fact]
    public void Resolve_ReplanDecision_ShouldReturnReplanAction()
    {
        // Arrange
        var newSubgoals = new List<string> { "Goal 1", "Goal 2" };
        var decision = new ReplanDecision(newSubgoals);
        var policy = CreateDefaultPolicy();
        var state = CreateTestState();

        // Act
        var action = _sut.Resolve(decision, null, policy, state);

        // Assert
        action.Should().BeOfType<ReplanAction>();
        var replanAction = action as ReplanAction;
        replanAction!.NewSubgoals.Should().BeEquivalentTo(newSubgoals);
    }

    [Fact]
    public void Resolve_AskUserDecision_ShouldReturnAskUserAction()
    {
        // Arrange
        var decision = new AskUserDecision("What is your preference?", new[] { "field1" });
        var policy = CreateDefaultPolicy();
        var state = CreateTestState();

        // Act
        var action = _sut.Resolve(decision, null, policy, state);

        // Assert
        action.Should().BeOfType<AskUserAction>();
        var askUserAction = action as AskUserAction;
        askUserAction!.Question.Should().Be("What is your preference?");
        askUserAction.MissingFields.Should().BeEquivalentTo(new[] { "field1" });
    }

    [Fact]
    public void Resolve_WithFailedObservation_AndRetryableError_ShouldRetry()
    {
        // Arrange
        var toolCall = new ToolCall("test_tool", JsonNode.Parse("""{}"""));
        var decision = new CallToolDecision(toolCall);
        var observation = new Observation(
            Summary: "Tool failed",
            KeyFacts: new Dictionary<string, string>(),
            Affordances: new[] { "retry_with_backoff" },
            Raw: new ToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.Timeout,
                ErrorDetails: null,
                SchemaValidated: false,
                Attempt: 1
            )
        );
        var policy = new ErrorHandlingPolicy(
            MaxRetries: 3,
            BaseBackoff: TimeSpan.FromSeconds(1),
            UseFallbacks: false,
            AskUserWhenMissingFields: false
        );
        var state = CreateTestState() with { LastAction = toolCall };

        // Act
        var action = _sut.Resolve(decision, observation, policy, state);

        // Assert
        action.Should().BeOfType<CallToolAction>();
        var retryAction = action as CallToolAction;
        retryAction!.Call.Should().Be(toolCall);
        // Verify it's the same call (retry behavior)
    }

    [Fact]
    public void Resolve_WithFailedObservation_ExceededRetries_ShouldStop()
    {
        // Arrange
        var toolCall = new ToolCall("test_tool", JsonNode.Parse("""{}"""));
        var decision = new CallToolDecision(toolCall);
        var observation = new Observation(
            Summary: "Tool failed",
            KeyFacts: new Dictionary<string, string>(),
            Affordances: new[] { "retry_with_backoff" },
            Raw: new ToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.Timeout,
                ErrorDetails: null,
                SchemaValidated: false,
                Attempt: 4 // Exceeded max retries
            )
        );
        var policy = new ErrorHandlingPolicy(
            MaxRetries: 3,
            BaseBackoff: TimeSpan.FromSeconds(1),
            UseFallbacks: false,
            AskUserWhenMissingFields: false
        );
        var state = CreateTestState() with { LastAction = toolCall };

        // Act
        var action = _sut.Resolve(decision, observation, policy, state);

        // Assert
        action.Should().BeOfType<StopAction>();
        var stopAction = action as StopAction;
        stopAction!.Reason.Should().Contain("Max retries exceeded");
    }

    [Fact]
    public void Resolve_WithInvalidInput_AndAskUserPolicy_ShouldAskUser()
    {
        // Arrange
        var toolCall = new ToolCall("test_tool", JsonNode.Parse("""{}"""));
        var decision = new CallToolDecision(toolCall);
        var observation = new Observation(
            Summary: "Invalid input",
            KeyFacts: new Dictionary<string, string>(),
            Affordances: new[] { "fix_parameters", "ask_user_for_clarification" },
            Raw: new ToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.InvalidInput,
                ErrorDetails: "Missing required field: location",
                SchemaValidated: false,
                Attempt: 1
            )
        );
        var policy = new ErrorHandlingPolicy(
            MaxRetries: 3,
            BaseBackoff: TimeSpan.FromSeconds(1),
            UseFallbacks: false,
            AskUserWhenMissingFields: true
        );
        var state = CreateTestState() with { LastAction = toolCall };

        // Act
        var action = _sut.Resolve(decision, observation, policy, state);

        // Assert
        action.Should().BeOfType<AskUserAction>();
        var askUserAction = action as AskUserAction;
        askUserAction!.Question.Should().Contain("Tool 'test_tool' failed with invalid input. Please provide correct parameters.");
    }

    [Fact]
    public void ShouldRetry_WithRetryableErrorCode_ReturnsTrue()
    {
        // Arrange
        var observation = new Observation(
            Summary: "Tool failed",
            KeyFacts: new Dictionary<string, string>(),
            Affordances: new List<string>(),
            Raw: new ToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.RetryableServer,
                ErrorDetails: null,
                SchemaValidated: false,
                Attempt: 1
            )
        );

        // Act
        // This is internal logic - test through Resolve method instead
        var decision = new CallToolDecision(new ToolCall("test", JsonNode.Parse("{}")!));
        var policy = new ErrorHandlingPolicy(3, TimeSpan.FromSeconds(1), false, false);
        var state = CreateTestState();
        var action = _sut.Resolve(decision, observation, policy, state);
        var shouldRetry = action is CallToolAction;

        // Assert
        shouldRetry.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_WithNonRetryableError_ReturnsFalse()
    {
        // Arrange
        var observation = new Observation(
            Summary: "Tool failed",
            KeyFacts: new Dictionary<string, string>(),
            Affordances: new List<string>(),
            Raw: new ToolResult(
                Ok: false,
                Data: null,
                ErrorCode: ToolErrorCode.ToolBug,
                ErrorDetails: null,
                SchemaValidated: false,
                Attempt: 1
            )
        );

        // Act
        // This is internal logic - test through Resolve method instead
        var decision = new CallToolDecision(new ToolCall("test", JsonNode.Parse("{}")!));
        var policy = new ErrorHandlingPolicy(3, TimeSpan.FromSeconds(1), false, false);
        var state = CreateTestState();
        var action = _sut.Resolve(decision, observation, policy, state);
        var shouldRetry = action is CallToolAction;

        // Assert
        shouldRetry.Should().BeFalse();
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

    private static ErrorHandlingPolicy CreateDefaultPolicy()
    {
        return new ErrorHandlingPolicy(
            MaxRetries: 3,
            BaseBackoff: TimeSpan.FromSeconds(1),
            UseFallbacks: false,
            AskUserWhenMissingFields: false
        );
    }
}