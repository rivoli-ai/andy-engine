using Andy.Engine.Contracts;
using Andy.Engine.Critic;
using Andy.Engine.Executor;
using Andy.Engine.Interactive;
using Andy.Engine.Normalizer;
using Andy.Engine.Planner;
using Andy.Engine.Policy;
using Andy.Engine.State;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Engine.Tests.Interactive;

/// <summary>
/// Tests for InteractiveAgent functionality
/// </summary>
public class InteractiveAgentTests
{
    private readonly Agent _agent;
    private readonly Mock<IUserInterface> _mockUserInterface;
    private readonly Mock<ILogger<InteractiveAgent>> _mockLogger;
    private readonly Mock<IPlanner> _mockPlanner;
    private readonly InteractiveAgent _sut;

    public InteractiveAgentTests()
    {
        // Create real Agent with mocked dependencies
        _mockPlanner = new Mock<IPlanner>();
        var mockExecutor = new Mock<IExecutor>();
        var mockCritic = new Mock<ICritic>();
        var mockNormalizer = new Mock<IObservationNormalizer>();

        _agent = new Agent(
            _mockPlanner.Object,
            mockExecutor.Object,
            mockCritic.Object,
            mockNormalizer.Object,
            new PolicyEngine(),
            new StateManager(new InMemoryStateStore()),
            Mock.Of<ILogger<Agent>>());

        _mockUserInterface = new Mock<IUserInterface>();
        _mockLogger = new Mock<ILogger<InteractiveAgent>>();

        var options = new InteractiveAgentOptions
        {
            WelcomeMessage = "Welcome to test!",
            ShowInitialHelp = false
        };

        _sut = new InteractiveAgent(_agent, _mockUserInterface.Object, options, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Act & Assert
        _sut.Should().NotBeNull();
        _sut.SessionId.Should().NotBeEmpty();
        _sut.History.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullAgent_ShouldThrow()
    {
        // Act & Assert
        var action = () => new InteractiveAgent(null!, _mockUserInterface.Object);
        action.Should().Throw<ArgumentNullException>().WithMessage("*agent*");
    }

    [Fact]
    public void Constructor_WithNullUserInterface_ShouldThrow()
    {
        // Act & Assert
        var action = () => new InteractiveAgent(_agent, null!);
        action.Should().Throw<ArgumentNullException>().WithMessage("*userInterface*");
    }

    [Fact]
    public async Task ProcessMessageAsync_WithValidMessage_ShouldCreateConversationTurn()
    {
        // Arrange
        var userMessage = "Please help me with something";

        // Setup planner to return a stop decision
        _mockPlanner.Setup(p => p.DecideAsync(It.IsAny<AgentState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StopDecision("Goal achieved successfully"));

        // Act
        var result = await _sut.ProcessMessageAsync(userMessage);

        // Assert
        result.Should().NotBeNull();
        _sut.History.Should().HaveCount(1);
        _sut.History[0].UserMessage.Should().Be(userMessage);
        _sut.History[0].AgentResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_WithEmptyMessage_ShouldThrow()
    {
        // Act & Assert
        var action = async () => await _sut.ProcessMessageAsync("");
        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*User message cannot be empty*");
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenPlannerThrows_ShouldReturnFailedResult()
    {
        // Arrange
        var userMessage = "This will fail";
        var exception = new InvalidOperationException("Planner failed");

        _mockPlanner.Setup(p => p.DecideAsync(It.IsAny<AgentState>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _sut.ProcessMessageAsync(userMessage);

        // Assert
        result.Success.Should().BeFalse();
        result.StopReason.Should().Contain("Error: Planner failed");
        _sut.History.Should().HaveCount(1);
        _sut.History[0].AgentResult.Should().Be(result);
    }

    [Fact]
    public async Task ClearConversationAsync_ShouldClearHistoryAndNotifyUser()
    {
        // Arrange
        await _sut.ProcessMessageAsync("Test message");
        _sut.History.Should().HaveCount(1);

        // Act
        await _sut.ClearConversationAsync();

        // Assert
        _sut.History.Should().BeEmpty();
        _mockUserInterface.Verify(ui => ui.ShowAsync(
            "Conversation cleared!",
            MessageType.Success,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShowStatsAsync_ShouldDisplayConversationStatistics()
    {
        // Act
        await _sut.ShowStatsAsync();

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowContentAsync(
            It.Is<string>(content =>
                content.Contains("Conversation Statistics") &&
                content.Contains("Turns: 0")),
            ContentType.Markdown,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithQuitCommand_ShouldExit()
    {
        // Arrange
        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert (should not hang)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Verify welcome message was shown
        _mockUserInterface.Verify(ui => ui.ShowAsync(
            "Welcome to test!",
            MessageType.Information,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithHelpCommand_ShouldShowHelp()
    {
        // Arrange
        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/help")
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInterface.Setup(ui => ui.ShowContentAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowContentAsync(
            It.Is<string>(content => content.Contains("Available Commands")),
            ContentType.Markdown,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithClearCommand_ShouldClearConversation()
    {
        // Arrange
        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/clear")
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowAsync(
            "Conversation cleared!",
            MessageType.Success,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithStatsCommand_ShouldShowStats()
    {
        // Arrange
        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/stats")
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInterface.Setup(ui => ui.ShowContentAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowContentAsync(
            It.Is<string>(content => content.Contains("Conversation Statistics")),
            ContentType.Markdown,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithHistoryCommand_ShouldShowHistory()
    {
        // Arrange
        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/history")
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInterface.Setup(ui => ui.ShowContentAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowContentAsync(
            It.Is<string>(content => content.Contains("Conversation History")),
            ContentType.Markdown,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithUnknownCommand_ShouldShowWarning()
    {
        // Arrange
        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/unknown")
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowAsync(
            It.Is<string>(msg => msg.Contains("Unknown command: /unknown")),
            MessageType.Warning,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSessionAsync_WithRegularMessage_ShouldProcessMessage()
    {
        // Arrange
        var userMessage = "Regular user message";

        // Setup planner to return a stop decision
        _mockPlanner.Setup(p => p.DecideAsync(It.IsAny<AgentState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StopDecision("Goal achieved successfully"));

        _mockUserInterface.SetupSequence(ui => ui.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMessage)
            .ReturnsAsync("/quit");

        _mockUserInterface.Setup(ui => ui.ShowAsync(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInterface.Setup(ui => ui.ShowProgressAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInterface.Setup(ui => ui.ShowContentAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.RunSessionAsync(cts.Token);

        // Assert
        _mockUserInterface.Verify(ui => ui.ShowProgressAsync(
            "Processing your request...",
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUserInterface.Verify(ui => ui.ShowProgressAsync(
            "Completed successfully!",
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act & Assert
        var action = () => _sut.Dispose();
        action.Should().NotThrow();
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