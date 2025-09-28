using Andy.Engine.Contracts;
using Andy.Engine.Interactive;
using FluentAssertions;
using Xunit;

namespace Andy.Engine.Tests.Interactive;

/// <summary>
/// Tests for ConversationManager functionality
/// </summary>
public class ConversationManagerTests
{
    private readonly ConversationManager _sut;

    public ConversationManagerTests()
    {
        _sut = new ConversationManager();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var manager = new ConversationManager();

        // Assert
        manager.SessionId.Should().NotBeEmpty();
        manager.History.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithOptions_ShouldUseProvidedOptions()
    {
        // Arrange
        var options = new ConversationOptions
        {
            MaxHistoryTurns = 50
        };

        // Act
        var manager = new ConversationManager(options);

        // Assert
        manager.SessionId.Should().NotBeEmpty();
        manager.History.Should().BeEmpty();
    }

    [Fact]
    public void AddUserMessage_ShouldCreateNewTurn()
    {
        // Arrange
        var message = "Hello, how can you help me?";

        // Act
        _sut.AddUserMessage(message);

        // Assert
        _sut.History.Should().HaveCount(1);
        var turn = _sut.History.First();
        turn.UserMessage.Should().Be(message);
        turn.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        turn.AgentResult.Should().BeNull();
        turn.Summary.Should().BeNull();
        turn.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void AddUserMessage_Multiple_ShouldCreateMultipleTurns()
    {
        // Arrange
        var messages = new[] { "First message", "Second message", "Third message" };

        // Act
        foreach (var message in messages)
        {
            _sut.AddUserMessage(message);
        }

        // Assert
        _sut.History.Should().HaveCount(3);
        for (int i = 0; i < messages.Length; i++)
        {
            _sut.History[i].UserMessage.Should().Be(messages[i]);
        }
    }

    [Fact]
    public void CompleteCurrentTurn_WithResult_ShouldUpdateTurn()
    {
        // Arrange
        _sut.AddUserMessage("Test message");
        var result = CreateSuccessfulResult();

        // Act
        _sut.CompleteCurrentTurn(result);

        // Assert
        _sut.History.Should().HaveCount(1);
        var turn = _sut.History.First();
        turn.AgentResult.Should().Be(result);
        turn.CompletedAt.Should().NotBeNull();
        turn.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        turn.Summary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CompleteCurrentTurn_WithCustomSummary_ShouldUseCustomSummary()
    {
        // Arrange
        _sut.AddUserMessage("Test message");
        var result = CreateSuccessfulResult();
        var customSummary = "Custom summary for this turn";

        // Act
        _sut.CompleteCurrentTurn(result, customSummary);

        // Assert
        var turn = _sut.History.First();
        turn.Summary.Should().Be(customSummary);
    }

    [Fact]
    public void CompleteCurrentTurn_WithoutActiveMessage_ShouldThrow()
    {
        // Arrange
        var result = CreateSuccessfulResult();

        // Act & Assert
        var action = () => _sut.CompleteCurrentTurn(result);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("No active conversation turn to complete");
    }

    [Fact]
    public void GetStats_WithNoHistory_ShouldReturnEmptyStats()
    {
        // Act
        var stats = _sut.GetStats();

        // Assert
        stats.SessionId.Should().Be(_sut.SessionId);
        stats.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        stats.TotalTurns.Should().Be(0);
        stats.CompletedTurns.Should().Be(0);
        stats.SuccessRate.Should().Be(0);
        stats.TotalDuration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        stats.AverageTurnDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetStats_WithMixedResults_ShouldCalculateCorrectly()
    {
        // Arrange
        _sut.AddUserMessage("Message 1");
        _sut.CompleteCurrentTurn(CreateSuccessfulResult());

        _sut.AddUserMessage("Message 2");
        _sut.CompleteCurrentTurn(CreateFailedResult());

        _sut.AddUserMessage("Message 3");
        _sut.CompleteCurrentTurn(CreateSuccessfulResult());

        // Act
        var stats = _sut.GetStats();

        // Assert
        stats.TotalTurns.Should().Be(3);
        stats.CompletedTurns.Should().Be(3);
        stats.SuccessRate.Should().BeApproximately(0.67, 0.01); // 2/3 = 0.67
        stats.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        stats.AverageTurnDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetConversationSummary_WithNoHistory_ShouldReturnEmptyMessage()
    {
        // Act
        var summary = _sut.GetConversationSummary();

        // Assert
        summary.Should().Contain("No conversation history.");
    }

    [Fact]
    public void GetConversationSummary_WithHistory_ShouldIncludeTurnInfo()
    {
        // Arrange
        _sut.AddUserMessage("First question");
        _sut.CompleteCurrentTurn(CreateSuccessfulResult(), "Successfully completed first task");

        _sut.AddUserMessage("Second question");
        _sut.CompleteCurrentTurn(CreateFailedResult(), "Failed to complete second task");

        // Act
        var summary = _sut.GetConversationSummary();

        // Assert
        summary.Should().Contain("First question");
        summary.Should().Contain("Successfully completed first task");
        summary.Should().Contain("Second question");
        summary.Should().Contain("Failed to complete second task");
        summary.Should().Contain("✓"); // Success indicator
        summary.Should().Contain("✗"); // Failure indicator
    }

    [Fact]
    public void Clear_ShouldResetAllState()
    {
        // Arrange
        _sut.AddUserMessage("Test message");
        _sut.CompleteCurrentTurn(CreateSuccessfulResult());
        var originalSessionId = _sut.SessionId;

        // Act
        _sut.Clear();

        // Assert
        _sut.History.Should().BeEmpty();
        _sut.SessionId.Should().NotBe(originalSessionId);
    }

    [Fact]
    public void MaxHistoryTurns_ShouldLimitHistorySize()
    {
        // Arrange
        var options = new ConversationOptions { MaxHistoryTurns = 2 };
        var manager = new ConversationManager(options);

        // Act - Add 3 turns, should only keep the last 2
        manager.AddUserMessage("Message 1");
        manager.CompleteCurrentTurn(CreateSuccessfulResult());

        manager.AddUserMessage("Message 2");
        manager.CompleteCurrentTurn(CreateSuccessfulResult());

        manager.AddUserMessage("Message 3");
        manager.CompleteCurrentTurn(CreateSuccessfulResult());

        // Assert
        manager.History.Should().HaveCount(2);
        manager.History[0].UserMessage.Should().Be("Message 2");
        manager.History[1].UserMessage.Should().Be("Message 3");
    }

    private static AgentResult CreateSuccessfulResult()
    {
        return new AgentResult(
            Success: true,
            StopReason: "Task completed successfully",
            TotalTurns: 1,
            Duration: TimeSpan.FromSeconds(2),
            FinalState: CreateTestState()
        );
    }

    private static AgentResult CreateFailedResult()
    {
        return new AgentResult(
            Success: false,
            StopReason: "Task failed",
            TotalTurns: 1,
            Duration: TimeSpan.FromSeconds(1),
            FinalState: CreateTestState()
        );
    }

    private static AgentState CreateTestState()
    {
        return new AgentState(
            Goal: new AgentGoal("Test goal", Array.Empty<string>()),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: new Observation(
                Summary: "Test observation",
                KeyFacts: new Dictionary<string, string>(),
                Affordances: new List<string>(),
                Raw: null!
            ),
            Budget: new Budget(10, TimeSpan.FromMinutes(5)),
            TurnIndex: 0,
            WorkingMemoryDigest: new Dictionary<string, string>()
        );
    }
}