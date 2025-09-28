using Andy.Engine.Interactive;
using Andy.Model.Llm;
using Andy.Tools.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Engine.Tests.Interactive;

/// <summary>
/// Tests for InteractiveAgentBuilder functionality
/// </summary>
public class InteractiveAgentBuilderTests
{
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IToolExecutor> _mockToolExecutor;
    private readonly Mock<IUserInterface> _mockUserInterface;
    private readonly Mock<ILogger<InteractiveAgent>> _mockLogger;

    public InteractiveAgentBuilderTests()
    {
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockToolExecutor = new Mock<IToolExecutor>();
        _mockUserInterface = new Mock<IUserInterface>();
        _mockLogger = new Mock<ILogger<InteractiveAgent>>();

        // Setup basic mock properties
        _mockLlmProvider.Setup(p => p.Name).Returns("TestProvider");
    }

    [Fact]
    public void Create_ShouldReturnNewBuilder()
    {
        // Act
        var builder = InteractiveAgentBuilder.Create();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<InteractiveAgentBuilder>();
    }

    [Fact]
    public void WithLlmProvider_ShouldSetProvider()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();

        // Act
        var result = builder.WithLlmProvider(_mockLlmProvider.Object);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithTools_ShouldSetToolComponents()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();

        // Act
        var result = builder.WithTools(_mockToolRegistry.Object, _mockToolExecutor.Object);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithUserInterface_ShouldSetUserInterface()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();

        // Act
        var result = builder.WithUserInterface(_mockUserInterface.Object);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithConsoleInterface_ShouldCreateConsoleInterface()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();

        // Act
        var result = builder.WithConsoleInterface();

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithConsoleInterface_WithOptions_ShouldUseProvidedOptions()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();
        var options = new ConsoleUserInterfaceOptions
        {
            UseColors = false,
            InputPrompt = "Custom> "
        };

        // Act
        var result = builder.WithConsoleInterface(options);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithOptions_ShouldSetInteractiveAgentOptions()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();
        var options = new InteractiveAgentOptions
        {
            WelcomeMessage = "Custom welcome message",
            ShowInitialHelp = true
        };

        // Act
        var result = builder.WithOptions(options);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithDefaults_ShouldSetAllRequiredComponents()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();

        // Act
        var result = builder.WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void WithLogger_ShouldSetLogger()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create();

        // Act
        var result = builder.WithLogger(_mockLogger.Object);

        // Assert
        result.Should().Be(builder); // Should return same instance for fluent interface
    }

    [Fact]
    public void Build_WithAllRequiredComponents_ShouldCreateInteractiveAgent()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create()
            .WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object)
            .WithUserInterface(_mockUserInterface.Object);

        // Act
        var agent = builder.Build();

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
        agent.SessionId.Should().NotBeEmpty();
    }

    [Fact]
    public void Build_WithoutLlmProvider_ShouldThrow()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create()
            .WithTools(_mockToolRegistry.Object, _mockToolExecutor.Object);

        // Act & Assert
        var action = () => builder.Build();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*LLM provider is required*");
    }

    [Fact]
    public void Build_WithoutToolRegistry_ShouldThrow()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create()
            .WithLlmProvider(_mockLlmProvider.Object);

        // Act & Assert
        var action = () => builder.Build();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tool registry is required*");
    }

    [Fact]
    public void Build_WithoutToolExecutor_ShouldThrow()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create()
            .WithLlmProvider(_mockLlmProvider.Object)
            .WithTools(_mockToolRegistry.Object, null!);

        // Act & Assert
        var action = () => builder.Build();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tool executor is required*");
    }

    [Fact]
    public void Build_WithoutUserInterface_ShouldUseConsoleInterfaceAsDefault()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create()
            .WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object);

        // Act
        var agent = builder.Build();

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
    }

    [Fact]
    public void Build_WithPlannerOptions_ShouldUseCustomPlannerOptions()
    {
        // Arrange
        var plannerOptions = new Andy.Engine.Planner.PlannerOptions
        {
            Temperature = 0.5,
            MaxTokens = 2000
        };

        var builder = InteractiveAgentBuilder.Create()
            .WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object)
            .WithPlannerOptions(plannerOptions);

        // Act
        var agent = builder.Build();

        // Assert
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithCriticOptions_ShouldUseCustomCriticOptions()
    {
        // Arrange
        var criticOptions = new Andy.Engine.Critic.CriticOptions
        {
            Temperature = 0.2,
            MaxTokens = 500
        };

        var builder = InteractiveAgentBuilder.Create()
            .WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object)
            .WithCriticOptions(criticOptions);

        // Act
        var agent = builder.Build();

        // Assert
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithAgentLogger_ShouldUseProvidedLogger()
    {
        // Arrange
        var agentLogger = Mock.Of<ILogger<Andy.Engine.Agent>>();
        var builder = InteractiveAgentBuilder.Create()
            .WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object)
            .WithAgentLogger(agentLogger);

        // Act
        var agent = builder.Build();

        // Assert
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Build_MultipleCallsWithSameBuilder_ShouldCreateDifferentAgents()
    {
        // Arrange
        var builder = InteractiveAgentBuilder.Create()
            .WithDefaults(_mockLlmProvider.Object, _mockToolRegistry.Object, _mockToolExecutor.Object);

        // Act
        var agent1 = builder.Build();
        var agent2 = builder.Build();

        // Assert
        agent1.Should().NotBeNull();
        agent2.Should().NotBeNull();
        agent1.Should().NotBeSameAs(agent2);
        agent1.SessionId.Should().NotBe(agent2.SessionId);
    }

    [Fact]
    public void CreateConsoleAgent_ShouldReturnConfiguredAgent()
    {
        // Arrange
        var options = new InteractiveAgentOptions
        {
            WelcomeMessage = "Welcome to console agent!"
        };

        // Act
        var agent = InteractiveAgentExtensions.CreateConsoleAgent(
            _mockLlmProvider.Object,
            _mockToolRegistry.Object,
            _mockToolExecutor.Object,
            options,
            _mockLogger.Object);

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
    }

    [Fact]
    public void CreateConsoleAgent_WithNullOptions_ShouldUseDefaults()
    {
        // Act
        var agent = InteractiveAgentExtensions.CreateConsoleAgent(
            _mockLlmProvider.Object,
            _mockToolRegistry.Object,
            _mockToolExecutor.Object);

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
    }

    [Fact]
    public void CreateCustomAgent_ShouldReturnConfiguredAgent()
    {
        // Arrange
        var options = new InteractiveAgentOptions
        {
            WelcomeMessage = "Welcome to custom agent!"
        };

        // Act
        var agent = InteractiveAgentExtensions.CreateCustomAgent(
            _mockLlmProvider.Object,
            _mockToolRegistry.Object,
            _mockToolExecutor.Object,
            _mockUserInterface.Object,
            options,
            _mockLogger.Object);

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
    }

    [Fact]
    public void CreateCustomAgent_WithNullOptions_ShouldUseDefaults()
    {
        // Act
        var agent = InteractiveAgentExtensions.CreateCustomAgent(
            _mockLlmProvider.Object,
            _mockToolRegistry.Object,
            _mockToolExecutor.Object,
            _mockUserInterface.Object);

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
    }

    [Fact]
    public void FluentInterface_ShouldAllowChaining()
    {
        // Arrange & Act
        var agent = InteractiveAgentBuilder.Create()
            .WithLlmProvider(_mockLlmProvider.Object)
            .WithTools(_mockToolRegistry.Object, _mockToolExecutor.Object)
            .WithConsoleInterface()
            .WithOptions(new InteractiveAgentOptions { WelcomeMessage = "Test" })
            .WithLogger(_mockLogger.Object)
            .Build();

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<InteractiveAgent>();
    }
}