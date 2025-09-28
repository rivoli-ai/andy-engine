using Andy.Engine.Contracts;
using Andy.Engine.Critic;
using Andy.Engine.Executor;
using Andy.Engine.Normalizer;
using Andy.Engine.Planner;
using Andy.Engine.Policy;
using Andy.Engine.State;
using Andy.Engine.Validation;
using Andy.Model.Llm;
using Andy.Tools.Core;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.Interactive;

/// <summary>
/// Builder for creating InteractiveAgent instances with fluent configuration
/// </summary>
public class InteractiveAgentBuilder
{
    private ILlmProvider? _llmProvider;
    private IToolRegistry? _toolRegistry;
    private IToolExecutor? _toolExecutor;
    private IUserInterface? _userInterface;
    private InteractiveAgentOptions? _options;
    private PlannerOptions? _plannerOptions;
    private CriticOptions? _criticOptions;
    private ILogger<InteractiveAgent>? _logger;
    private ILogger<Agent>? _agentLogger;

    /// <summary>
    /// Create a new InteractiveAgent builder
    /// </summary>
    public static InteractiveAgentBuilder Create() => new();

    /// <summary>
    /// Configure the LLM provider for the agent
    /// </summary>
    public InteractiveAgentBuilder WithLlmProvider(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
        return this;
    }

    /// <summary>
    /// Configure the tool registry and executor
    /// </summary>
    public InteractiveAgentBuilder WithTools(IToolRegistry toolRegistry, IToolExecutor toolExecutor)
    {
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        return this;
    }

    /// <summary>
    /// Configure the user interface implementation
    /// </summary>
    public InteractiveAgentBuilder WithUserInterface(IUserInterface userInterface)
    {
        _userInterface = userInterface;
        return this;
    }

    /// <summary>
    /// Use the default console user interface
    /// </summary>
    public InteractiveAgentBuilder WithConsoleInterface(ConsoleUserInterfaceOptions? options = null)
    {
        _userInterface = new ConsoleUserInterface(options);
        return this;
    }

    /// <summary>
    /// Configure interactive agent options
    /// </summary>
    public InteractiveAgentBuilder WithOptions(InteractiveAgentOptions options)
    {
        _options = options;
        return this;
    }

    /// <summary>
    /// Configure planner options
    /// </summary>
    public InteractiveAgentBuilder WithPlannerOptions(PlannerOptions options)
    {
        _plannerOptions = options;
        return this;
    }

    /// <summary>
    /// Configure critic options
    /// </summary>
    public InteractiveAgentBuilder WithCriticOptions(CriticOptions options)
    {
        _criticOptions = options;
        return this;
    }

    /// <summary>
    /// Configure logging
    /// </summary>
    public InteractiveAgentBuilder WithLogger(ILogger<InteractiveAgent> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Configure agent logging
    /// </summary>
    public InteractiveAgentBuilder WithAgentLogger(ILogger<Agent> agentLogger)
    {
        _agentLogger = agentLogger;
        return this;
    }

    /// <summary>
    /// Configure defaults using existing providers (similar to AgentBuilder)
    /// </summary>
    public InteractiveAgentBuilder WithDefaults(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor)
    {
        _llmProvider = llmProvider;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        return this;
    }

    /// <summary>
    /// Build the InteractiveAgent with configured settings
    /// </summary>
    public InteractiveAgent Build()
    {
        // Validate required dependencies
        if (_llmProvider == null)
            throw new InvalidOperationException("LLM provider is required. Use WithLlmProvider() or WithDefaults().");

        if (_toolRegistry == null)
            throw new InvalidOperationException("Tool registry is required. Use WithTools() or WithDefaults().");

        if (_toolExecutor == null)
            throw new InvalidOperationException("Tool executor is required. Use WithTools() or WithDefaults().");

        if (_userInterface == null)
        {
            // Default to console interface
            _userInterface = new ConsoleUserInterface();
        }

        // Build core agent components
        var planner = new LlmPlanner(_llmProvider, _toolRegistry, _plannerOptions);
        var critic = new LlmCritic(_llmProvider, _criticOptions);
        var validator = new JsonSchemaValidator();
        var toolAdapter = new ToolAdapter(_toolRegistry, _toolExecutor, validator);
        var normalizer = new DefaultObservationNormalizer();
        var policyEngine = new PolicyEngine();
        var stateStore = new InMemoryStateStore();
        var stateManager = new StateManager(stateStore);

        // Build core agent
        var agent = new Agent(
            planner,
            toolAdapter,
            critic,
            normalizer,
            policyEngine,
            stateManager,
            _agentLogger
        );

        // Build interactive agent
        return new InteractiveAgent(
            agent,
            _userInterface,
            _options,
            _logger
        );
    }
}

/// <summary>
/// Extension methods for common InteractiveAgent configurations
/// </summary>
public static class InteractiveAgentExtensions
{
    /// <summary>
    /// Create a simple console-based interactive agent
    /// </summary>
    public static InteractiveAgent CreateConsoleAgent(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        InteractiveAgentOptions? options = null,
        ILogger<InteractiveAgent>? logger = null)
    {
        return InteractiveAgentBuilder.Create()
            .WithDefaults(llmProvider, toolRegistry, toolExecutor)
            .WithConsoleInterface()
            .WithOptions(options ?? InteractiveAgentOptions.Default)
            .WithLogger(logger)
            .Build();
    }

    /// <summary>
    /// Create an interactive agent with custom user interface
    /// </summary>
    public static InteractiveAgent CreateCustomAgent(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        IUserInterface userInterface,
        InteractiveAgentOptions? options = null,
        ILogger<InteractiveAgent>? logger = null)
    {
        return InteractiveAgentBuilder.Create()
            .WithDefaults(llmProvider, toolRegistry, toolExecutor)
            .WithUserInterface(userInterface)
            .WithOptions(options ?? InteractiveAgentOptions.Default)
            .WithLogger(logger)
            .Build();
    }
}