using Andy.Engine.Interactive;
using Andy.Engine.Planner;
using Andy.Llm.Extensions;
using Andy.Llm.Providers;
using Andy.Llm.Services;
using Andy.Tools;
using Andy.Tools.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InteractiveConsoleAgent;

/// <summary>
/// Example demonstrating how to use Andy.Engine's Interactive Agent
/// This shows how andy-cli could be simplified to use Andy.Engine
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Andy.Engine Interactive Console Agent ===\n");

        try
        {
            // Setup services (similar to existing andy-cli setup)
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Information);
            });

            // Configure LLM from environment
            services.ConfigureLlmFromEnvironment();
            services.AddLlmServices(options =>
            {
                options.DefaultProvider = "openai"; // or auto-detect
            });

            // Add Andy Tools
            services.AddAndyTools(options =>
            {
                options.RegisterBuiltInTools = true;
                options.EnableDetailedTracing = false;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Initialize tool framework
            var lifecycleManager = serviceProvider.GetRequiredService<IToolLifecycleManager>();
            await lifecycleManager.InitializeAsync();

            // Get required services
            var llmFactory = serviceProvider.GetRequiredService<ILlmProviderFactory>();
            var llmProvider = await llmFactory.CreateAvailableProviderAsync();
            var toolRegistry = serviceProvider.GetRequiredService<Andy.Tools.Core.IToolRegistry>();
            var toolExecutor = serviceProvider.GetRequiredService<Andy.Tools.Core.IToolExecutor>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            Console.WriteLine($"Using LLM provider: {llmProvider.Name}");
            Console.WriteLine($"Available tools: {string.Join(", ", toolRegistry.GetTools().Select(t => t.Metadata.Name))}");
            Console.WriteLine();

            // Create interactive agent with custom options
            var options = new InteractiveAgentOptions
            {
                WelcomeMessage = "Welcome to Andy.Engine Interactive Agent! I can help you with various tasks using the available tools.",
                ShowInitialHelp = true,
                DefaultConstraints = new List<string>
                {
                    "Be helpful and accurate",
                    "Explain what tools you're using and why",
                    "Ask for clarification if the request is ambiguous"
                }
            };

            var consoleOptions = new ConsoleUserInterfaceOptions
            {
                UseColors = true,
                ShowMessagePrefixes = true,
                InputPrompt = "🤖 You: "
            };

            // Build the interactive agent
            using var interactiveAgent = InteractiveAgentBuilder.Create()
                .WithDefaults(llmProvider, toolRegistry, toolExecutor)
                .WithConsoleInterface(consoleOptions)
                .WithOptions(options)
                .WithPlannerOptions(new PlannerOptions
                {
                    Temperature = 0.1,
                    MaxTokens = 1000,
                    SystemPrompt = """
                        You are a helpful AI assistant with access to various tools.
                        Always respond with valid JSON for tool calls.
                        Be concise but thorough in your explanations.
                        """
                })
                .WithLogger(serviceProvider.GetRequiredService<ILogger<InteractiveAgent>>())
                .Build();

            // Start the interactive session
            await interactiveAgent.RunSessionAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No API key"))
        {
            Console.WriteLine("❌ No LLM API key configured!");
            Console.WriteLine("Please set one of the following environment variables:");
            Console.WriteLine("  - OPENAI_API_KEY for OpenAI");
            Console.WriteLine("  - CEREBRAS_API_KEY for Cerebras");
            Console.WriteLine("  - ANTHROPIC_API_KEY for Claude");
            Environment.Exit(1);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n👋 Session ended by user.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}