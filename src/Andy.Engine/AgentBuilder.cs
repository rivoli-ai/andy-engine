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
using Andy.Tools.Core.OutputLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Andy.Engine;

/// <summary>
/// Builder for configuring and creating Agent instances.
/// </summary>
public class AgentBuilder
{
    private IServiceProvider? _serviceProvider;
    private IPlanner? _planner;
    private IExecutor? _executor;
    private ICritic? _critic;
    private IObservationNormalizer? _normalizer;
    private PolicyEngine? _policyEngine;
    private StateManager? _stateManager;
    private ILogger<Agent>? _logger;

    // Component options
    private PlannerOptions? _plannerOptions;
    private CriticOptions? _criticOptions;
    private NormalizerOptions? _normalizerOptions;
    private StateOptions? _stateOptions;

    private AgentBuilder() { }

    /// <summary>
    /// Creates a new agent builder.
    /// </summary>
    public static AgentBuilder Create() => new();

    /// <summary>
    /// Uses a service provider for dependency injection.
    /// </summary>
    public AgentBuilder WithServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        return this;
    }

    /// <summary>
    /// Configures the agent to use default components with the provided dependencies.
    /// </summary>
    public AgentBuilder WithDefaults(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor)
    {
        var validator = new JsonSchemaValidator();
        var loggerFactory = _serviceProvider?.GetService<ILoggerFactory>();

        _planner = new LlmPlanner(
            llmProvider,
            toolRegistry,
            _plannerOptions,
            loggerFactory?.CreateLogger<LlmPlanner>()
        );

        _executor = new ToolAdapter(
            toolRegistry,
            toolExecutor,
            validator,
            loggerFactory?.CreateLogger<ToolAdapter>()
        );

        _critic = new LlmCritic(
            llmProvider,
            _criticOptions,
            loggerFactory?.CreateLogger<LlmCritic>()
        );

        _normalizer = new DefaultObservationNormalizer(
            _serviceProvider?.GetService<IToolOutputLimiter>(),
            _normalizerOptions,
            loggerFactory?.CreateLogger<DefaultObservationNormalizer>()
        );

        _policyEngine = new PolicyEngine(
            loggerFactory?.CreateLogger<PolicyEngine>()
        );

        var stateStore = _serviceProvider?.GetService<IStateStore>() ?? new InMemoryStateStore();
        _stateManager = new StateManager(
            stateStore,
            _stateOptions,
            loggerFactory?.CreateLogger<StateManager>()
        );

        _logger = loggerFactory?.CreateLogger<Agent>();

        return this;
    }

    /// <summary>
    /// Sets a custom planner.
    /// </summary>
    public AgentBuilder WithPlanner(IPlanner planner)
    {
        _planner = planner;
        return this;
    }

    /// <summary>
    /// Sets planner options.
    /// </summary>
    public AgentBuilder WithPlannerOptions(PlannerOptions options)
    {
        _plannerOptions = options;
        return this;
    }

    /// <summary>
    /// Sets a custom executor.
    /// </summary>
    public AgentBuilder WithExecutor(IExecutor executor)
    {
        _executor = executor;
        return this;
    }

    /// <summary>
    /// Sets a custom critic.
    /// </summary>
    public AgentBuilder WithCritic(ICritic critic)
    {
        _critic = critic;
        return this;
    }

    /// <summary>
    /// Sets critic options.
    /// </summary>
    public AgentBuilder WithCriticOptions(CriticOptions options)
    {
        _criticOptions = options;
        return this;
    }

    /// <summary>
    /// Sets a custom observation normalizer.
    /// </summary>
    public AgentBuilder WithNormalizer(IObservationNormalizer normalizer)
    {
        _normalizer = normalizer;
        return this;
    }

    /// <summary>
    /// Sets normalizer options.
    /// </summary>
    public AgentBuilder WithNormalizerOptions(NormalizerOptions options)
    {
        _normalizerOptions = options;
        return this;
    }

    /// <summary>
    /// Sets a custom policy engine.
    /// </summary>
    public AgentBuilder WithPolicyEngine(PolicyEngine policyEngine)
    {
        _policyEngine = policyEngine;
        return this;
    }

    /// <summary>
    /// Sets a custom state manager.
    /// </summary>
    public AgentBuilder WithStateManager(StateManager stateManager)
    {
        _stateManager = stateManager;
        return this;
    }

    /// <summary>
    /// Sets state manager options.
    /// </summary>
    public AgentBuilder WithStateOptions(StateOptions options)
    {
        _stateOptions = options;
        return this;
    }

    /// <summary>
    /// Sets the logger for the agent.
    /// </summary>
    public AgentBuilder WithLogger(ILogger<Agent> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Builds the configured agent.
    /// </summary>
    public Agent Build()
    {
        if (_planner == null)
            throw new InvalidOperationException("Planner is required. Call WithDefaults or WithPlanner.");

        if (_executor == null)
            throw new InvalidOperationException("Executor is required. Call WithDefaults or WithExecutor.");

        if (_critic == null)
            throw new InvalidOperationException("Critic is required. Call WithDefaults or WithCritic.");

        if (_normalizer == null)
            throw new InvalidOperationException("Normalizer is required. Call WithDefaults or WithNormalizer.");

        if (_policyEngine == null)
            throw new InvalidOperationException("PolicyEngine is required. Call WithDefaults or WithPolicyEngine.");

        if (_stateManager == null)
            throw new InvalidOperationException("StateManager is required. Call WithDefaults or WithStateManager.");

        return new Agent(
            _planner,
            _executor,
            _critic,
            _normalizer,
            _policyEngine,
            _stateManager,
            _logger
        );
    }
}