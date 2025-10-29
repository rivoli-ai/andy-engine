using Andy.Engine;
using Andy.Engine.Contracts;
using Andy.Engine.Planner;
using Andy.Llm.Extensions;
using Andy.Llm.Providers;
using Andy.Llm.Services;
using Andy.Tools;
using Andy.Tools.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SimpleAgent;

class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        // Configure services
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug);
        });

        // Configure LLM from configuration file, then environment variables will merge
        services.AddLlmServices(configuration);
        services.ConfigureLlmFromEnvironment();

        // Add Andy Tools framework with built-in tools
        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            options.EnableDetailedTracing = false; // Set to true for debugging
        });

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting Andy.Engine with real LLM and Tools");

            // Run direct tool test first
            await DirectToolTest.TestDirectToolExecution();
            Console.WriteLine("\n=== Starting main agent execution ===\n");

            // Initialize the tool framework (needed for console apps)
            var lifecycleManager = serviceProvider.GetRequiredService<IToolLifecycleManager>();
            await lifecycleManager.InitializeAsync();

            // Get the LLM provider
            var llmFactory = serviceProvider.GetRequiredService<ILlmProviderFactory>();
            var llmProvider = await llmFactory.CreateAvailableProviderAsync();
            logger.LogInformation("Using LLM provider: {Provider}", llmProvider.Name);

            // Get the tool registry and executor
            var toolRegistry = serviceProvider.GetRequiredService<Andy.Tools.Core.IToolRegistry>();
            var toolExecutor = serviceProvider.GetRequiredService<Andy.Tools.Core.IToolExecutor>();

            // Log available tools
            var availableTools = toolRegistry.GetTools();
            logger.LogInformation("Available tools: {Tools}",
                string.Join(", ", availableTools.Select(t => t.Metadata.Name)));

            // Build the agent using Andy.Engine
            var agent = AgentBuilder.Create()
                .WithDefaults(llmProvider, toolRegistry, toolExecutor)
                .WithPlannerOptions(new PlannerOptions
                {
                    Temperature = 0,
                    MaxTokens = 1000,
                    SystemPrompt = """
                        You are a JSON-only planning agent. Respond ONLY with valid JSON.

                        Output format (choose one):
                        {"action": "call_tool", "name": "tool_id", "args": {...}}
                        {"action": "stop", "reason": "completed"}

                        Rules:
                        - ONLY output JSON, nothing else
                        - Use exact tool IDs from the available tools list
                        - Use exact parameter names and allowed values as specified
                        - Always include "action" field
                        - For datetime_tool, use operation: "now" to get current time
                        - For encoding_tool, specify operation and input parameters
                        """
                })
                .WithLogger(serviceProvider.GetRequiredService<ILogger<Agent>>())
                .Build();

            // Subscribe to agent events
            agent.TurnStarted += (sender, e) =>
                logger.LogInformation("Turn {TurnNumber} started", e.TurnNumber);

            agent.TurnCompleted += (sender, e) =>
                logger.LogInformation("Turn {TurnNumber} completed: {ActionType}", e.TurnNumber, e.ActionType);

            agent.ToolCalled += (sender, e) =>
                logger.LogInformation("Tool called: {ToolName} - Result: {Result}",
                    e.ToolName, e.Result);

            // Define a goal for the agent - let's do something practical
            var goal = new AgentGoal(
                UserGoal: "Get the current date and time, then encode it to base64",
                Constraints: new[] {
                    "Use the datetime_tool to get the current time",
                    "Use the encoding_tool to encode the result to base64",
                    "Complete within 5 turns"
                }
            );

            // Set a budget
            var budget = new Budget(
                MaxTurns: 5,
                MaxWallClock: TimeSpan.FromMinutes(2)
            );

            // Configure error handling policy
            var errorPolicy = new ErrorHandlingPolicy(
                MaxRetries: 2,
                BaseBackoff: TimeSpan.FromSeconds(1),
                UseFallbacks: true,
                AskUserWhenMissingFields: false
            );

            logger.LogInformation("Running agent with goal: {Goal}", goal.UserGoal);
            logger.LogInformation("Constraints: {Constraints}", string.Join(", ", goal.Constraints));

            // Run the agent
            var result = await agent.RunAsync(goal, budget, errorPolicy);

            // Display results
            logger.LogInformation("========================================");
            logger.LogInformation("Agent execution completed");
            logger.LogInformation("Success: {Success}", result.Success);
            logger.LogInformation("Stop reason: {StopReason}", result.StopReason);
            logger.LogInformation("Turns taken: {TotalTurns}", result.TotalTurns);
            logger.LogInformation("Total time: {Duration:F2}s", result.Duration.TotalSeconds);

            if (result.FinalState.LastObservation != null)
            {
                logger.LogInformation("Final observation: {Summary}", result.FinalState.LastObservation.Summary);
                if (result.FinalState.LastObservation.KeyFacts.Count > 0)
                {
                    logger.LogInformation("Key facts:");
                    foreach (var fact in result.FinalState.LastObservation.KeyFacts)
                    {
                        logger.LogInformation("  {Key}: {Value}", fact.Key, fact.Value);
                    }
                }
            }

            // Display final state
            logger.LogInformation("Final state:");
            logger.LogInformation("  Goal: {Goal}", result.FinalState.Goal.UserGoal);
            logger.LogInformation("  Turn index: {TurnIndex}", result.FinalState.TurnIndex);
            if (result.FinalState.Subgoals.Count > 0)
            {
                logger.LogInformation("  Subgoals: {Subgoals}", string.Join(", ", result.FinalState.Subgoals));
            }
            if (result.FinalState.WorkingMemoryDigest.Count > 0)
            {
                logger.LogInformation("  Working memory:");
                foreach (var memory in result.FinalState.WorkingMemoryDigest.Take(5))
                {
                    logger.LogInformation("    {Key}: {Value}", memory.Key, memory.Value);
                }
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No API key"))
        {
            logger.LogError("No LLM API key configured!");
            logger.LogError("Please set one of the following environment variables:");
            logger.LogError("  - OPENAI_API_KEY for OpenAI");
            logger.LogError("  - CEREBRAS_API_KEY for Cerebras");
            logger.LogError("  - ANTHROPIC_API_KEY for Claude");
            logger.LogError("  - Or configure Ollama locally");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running agent");
            Environment.Exit(1);
        }
    }
}