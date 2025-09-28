using System.Text.Json;
using Andy.Engine.Contracts;
using Microsoft.Extensions.Logging;

namespace Andy.Engine.Interactive;

/// <summary>
/// Interactive agent that wraps the core Agent with conversation management
/// and user interface capabilities for multi-turn interactions
/// </summary>
public class InteractiveAgent : IDisposable
{
    private readonly Agent _agent;
    private readonly IUserInterface _userInterface;
    private readonly ConversationManager _conversationManager;
    private readonly ILogger<InteractiveAgent>? _logger;
    private readonly InteractiveAgentOptions _options;
    private bool _disposed;

    public InteractiveAgent(
        Agent agent,
        IUserInterface userInterface,
        InteractiveAgentOptions? options = null,
        ILogger<InteractiveAgent>? logger = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _userInterface = userInterface ?? throw new ArgumentNullException(nameof(userInterface));
        _options = options ?? InteractiveAgentOptions.Default;
        _logger = logger;

        _conversationManager = new ConversationManager(_options.ConversationOptions);

        // Subscribe to agent events for progress updates
        SubscribeToAgentEvents();
    }

    /// <summary>
    /// Current conversation session
    /// </summary>
    public string SessionId => _conversationManager.SessionId;

    /// <summary>
    /// Conversation history
    /// </summary>
    public IReadOnlyList<ConversationTurn> History => _conversationManager.History;

    /// <summary>
    /// Start an interactive session with the agent
    /// </summary>
    public async Task RunSessionAsync(CancellationToken cancellationToken = default)
    {
        await _userInterface.ShowAsync(_options.WelcomeMessage, MessageType.Information, cancellationToken);

        if (_options.ShowInitialHelp)
        {
            await ShowHelpAsync(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get user input
                var userInput = await _userInterface.AskAsync(_options.Prompt, cancellationToken);

                if (string.IsNullOrWhiteSpace(userInput))
                    continue;

                // Handle special commands
                if (await HandleSpecialCommandAsync(userInput, cancellationToken))
                    continue;

                // Process user message through agent
                await ProcessUserMessageAsync(userInput, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in interactive session");
                await _userInterface.ShowAsync($"An error occurred: {ex.Message}", MessageType.Error, cancellationToken);
            }
        }

        await _userInterface.ShowAsync(_options.GoodbyeMessage, MessageType.Information, cancellationToken);
    }

    /// <summary>
    /// Process a single user message (useful for API/single-turn interactions)
    /// </summary>
    public async Task<AgentResult> ProcessMessageAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be empty", nameof(userMessage));

        _conversationManager.AddUserMessage(userMessage);

        try
        {
            // Convert user message to agent goal
            var goal = await RefineUserGoalAsync(userMessage, cancellationToken);

            // Run agent with budget and error handling
            var budget = _options.DefaultBudget;
            var errorPolicy = _options.DefaultErrorPolicy;

            var result = await _agent.RunAsync(goal, budget, errorPolicy, cancellationToken);

            // Complete the conversation turn
            _conversationManager.CompleteCurrentTurn(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing user message: {Message}", userMessage);

            // Create a failed result
            var failedResult = new AgentResult(
                Success: false,
                StopReason: $"Error: {ex.Message}",
                TotalTurns: 0,
                Duration: TimeSpan.Zero,
                FinalState: CreateErrorState(userMessage, ex)
            );

            _conversationManager.CompleteCurrentTurn(failedResult);
            return failedResult;
        }
    }

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public async Task ClearConversationAsync(CancellationToken cancellationToken = default)
    {
        _conversationManager.Clear();
        await _userInterface.ShowAsync("Conversation cleared!", MessageType.Success, cancellationToken);
    }

    /// <summary>
    /// Show conversation statistics
    /// </summary>
    public async Task ShowStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = _conversationManager.GetStats();
        var message = $"""
            **Conversation Statistics**

            Session ID: {stats.SessionId}
            Started: {stats.StartedAt:yyyy-MM-dd HH:mm:ss}
            Duration: {stats.TotalDuration:hh\\:mm\\:ss}

            Turns: {stats.TotalTurns} total, {stats.CompletedTurns} completed
            Success Rate: {stats.SuccessRate:P1}
            Average Turn Time: {stats.AverageTurnDuration.TotalSeconds:F1}s
            """;

        await _userInterface.ShowContentAsync(message, ContentType.Markdown, cancellationToken);
    }

    private async Task ProcessUserMessageAsync(string userMessage, CancellationToken cancellationToken)
    {
        await _userInterface.ShowProgressAsync("Processing your request...", false, cancellationToken);

        var result = await ProcessMessageAsync(userMessage, cancellationToken);

        // Show result to user
        if (result.Success)
        {
            await _userInterface.ShowProgressAsync("Completed successfully!", true, cancellationToken);

            // Show final observation if available
            var observation = result.FinalState.LastObservation;
            if (observation != null && !string.IsNullOrEmpty(observation.Summary))
            {
                await _userInterface.ShowContentAsync(observation.Summary, ContentType.Markdown, cancellationToken);
            }

            // Show key facts if available
            if (observation?.KeyFacts.Count > 0)
            {
                var facts = string.Join("\n", observation.KeyFacts.Select(kv => $"- **{kv.Key}**: {kv.Value}"));
                await _userInterface.ShowContentAsync($"**Key Information:**\n{facts}", ContentType.Markdown, cancellationToken);
            }
        }
        else
        {
            await _userInterface.ShowAsync($"Task failed: {result.StopReason}", MessageType.Error, cancellationToken);

            // Offer to retry or clarify
            if (await _userInterface.ConfirmAsync("Would you like to try rephrasing your request?", false, cancellationToken))
            {
                var clarification = await _userInterface.AskAsync("How would you like to rephrase it?", cancellationToken);
                if (!string.IsNullOrWhiteSpace(clarification))
                {
                    await ProcessUserMessageAsync(clarification, cancellationToken);
                }
            }
        }
    }

    private async Task<bool> HandleSpecialCommandAsync(string input, CancellationToken cancellationToken)
    {
        if (!input.StartsWith("/"))
            return false;

        var parts = input.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var command = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (command)
        {
            case "help" or "?":
                await ShowHelpAsync(cancellationToken);
                return true;

            case "clear":
                await ClearConversationAsync(cancellationToken);
                return true;

            case "stats":
                await ShowStatsAsync(cancellationToken);
                return true;

            case "history":
                await ShowHistoryAsync(cancellationToken);
                return true;

            case "quit" or "exit":
                throw new OperationCanceledException("User requested exit");

            default:
                await _userInterface.ShowAsync($"Unknown command: /{command}. Type /help for available commands.", MessageType.Warning, cancellationToken);
                return true;
        }
    }

    private async Task ShowHelpAsync(CancellationToken cancellationToken)
    {
        var help = """
            **Available Commands:**

            - `/help` - Show this help message
            - `/clear` - Clear conversation history
            - `/stats` - Show conversation statistics
            - `/history` - Show recent conversation history
            - `/quit` or `/exit` - Exit the session

            **Usage:**
            Just type your requests naturally. I can help you with tasks using the available tools.
            """;

        await _userInterface.ShowContentAsync(help, ContentType.Markdown, cancellationToken);
    }

    private async Task ShowHistoryAsync(CancellationToken cancellationToken)
    {
        var summary = _conversationManager.GetConversationSummary();
        await _userInterface.ShowContentAsync($"**Conversation History:**\n\n{summary}", ContentType.Markdown, cancellationToken);
    }

    private async Task<AgentGoal> RefineUserGoalAsync(string userMessage, CancellationToken cancellationToken)
    {
        // Basic goal creation - could be enhanced with LLM-based goal refinement
        var constraints = new List<string>();

        // Add conversation context if available
        if (_conversationManager.History.Count > 0)
        {
            constraints.Add("Consider previous conversation context");
        }

        // Add any configured constraints
        constraints.AddRange(_options.DefaultConstraints);

        return new AgentGoal(
            UserGoal: userMessage,
            Constraints: constraints
        );
    }

    private AgentState CreateErrorState(string userMessage, Exception exception)
    {
        return new AgentState(
            Goal: new AgentGoal(userMessage, Array.Empty<string>()),
            Subgoals: new List<string>(),
            LastAction: null,
            LastObservation: new Observation(
                Summary: $"Error occurred: {exception.Message}",
                KeyFacts: new Dictionary<string, string>
                {
                    ["error"] = exception.Message
                },
                Affordances: new List<string> { "retry", "clarify", "cancel" },
                Raw: null!
            ),
            Budget: _options.DefaultBudget,
            TurnIndex: 0,
            WorkingMemoryDigest: new Dictionary<string, string>
            {
                ["error"] = exception.Message,
                ["error_type"] = exception.GetType().Name
            }
        );
    }

    private void SubscribeToAgentEvents()
    {
        _agent.TurnStarted += async (sender, e) =>
        {
            await _userInterface.ShowProgressAsync($"Turn {e.TurnNumber} started...", false);
        };

        _agent.TurnCompleted += async (sender, e) =>
        {
            await _userInterface.ShowProgressAsync($"Turn {e.TurnNumber} completed: {e.ActionType}", false);
        };

        _agent.ToolCalled += async (sender, e) =>
        {
            await _userInterface.ShowProgressAsync($"Using tool: {e.ToolName}", false);
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Unsubscribe from events to prevent memory leaks
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for the Interactive Agent
/// </summary>
public class InteractiveAgentOptions
{
    /// <summary>
    /// Message shown when starting a session
    /// </summary>
    public string WelcomeMessage { get; set; } = "Welcome! I'm your AI assistant. How can I help you today?";

    /// <summary>
    /// Message shown when ending a session
    /// </summary>
    public string GoodbyeMessage { get; set; } = "Goodbye! Have a great day!";

    /// <summary>
    /// Prompt shown to user for input
    /// </summary>
    public string Prompt { get; set; } = "You: ";

    /// <summary>
    /// Whether to show help on session start
    /// </summary>
    public bool ShowInitialHelp { get; set; } = false;

    /// <summary>
    /// Default budget for agent tasks
    /// </summary>
    public Budget DefaultBudget { get; set; } = new(MaxTurns: 10, MaxWallClock: TimeSpan.FromMinutes(5));

    /// <summary>
    /// Default error handling policy
    /// </summary>
    public ErrorHandlingPolicy DefaultErrorPolicy { get; set; } = new(
        MaxRetries: 2,
        BaseBackoff: TimeSpan.FromSeconds(1),
        UseFallbacks: true,
        AskUserWhenMissingFields: true
    );

    /// <summary>
    /// Default constraints applied to all goals
    /// </summary>
    public List<string> DefaultConstraints { get; set; } = new();

    /// <summary>
    /// Conversation management options
    /// </summary>
    public ConversationOptions ConversationOptions { get; set; } = ConversationOptions.Default;

    /// <summary>
    /// Default interactive agent options
    /// </summary>
    public static InteractiveAgentOptions Default => new();
}