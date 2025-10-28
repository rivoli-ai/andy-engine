using Andy.Engine;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Andy.Llm.Extensions;
using Andy.Llm.Services;
using Andy.Llm.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SimpleAgent;

/// <summary>
/// Program to run file system benchmarks with mocked and real LLM
/// </summary>
class BenchmarkProgram
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Andy.Engine File System Benchmarks");
        Console.WriteLine("===================================\n");

        // Check if we should use real LLM or mocked
        var useRealLlm = args.Length > 0 && args[0].ToLower() == "real";

        if (useRealLlm && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            Console.WriteLine("ERROR: OPENAI_API_KEY environment variable is not set.");
            Console.WriteLine("Please set it to run benchmarks with real OpenAI LLM.");
            Console.WriteLine("\nTo run with mocked LLM instead: dotnet run");
            Environment.Exit(1);
        }

        // Configure services
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        // Configure LLM services (only if using real LLM)
        if (useRealLlm)
        {
            services.ConfigureLlmFromEnvironment();
            services.AddLlmServices(options =>
            {
                options.DefaultProvider = "openai";
            });
        }

        // Add Andy Tools framework with built-in file system tools
        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            options.EnableDetailedTracing = false;
        });

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<BenchmarkProgram>>();

        try
        {
            logger.LogInformation("Starting File System Benchmarks");
            logger.LogInformation("Mode: {Mode}", useRealLlm ? "REAL OpenAI LLM" : "MOCKED LLM");

            // Initialize the tool framework
            var lifecycleManager = serviceProvider.GetRequiredService<IToolLifecycleManager>();
            await lifecycleManager.InitializeAsync();

            // Get tool registry and executor
            var toolRegistry = serviceProvider.GetRequiredService<IToolRegistry>();
            var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

            // Log available tools
            var availableTools = toolRegistry.GetTools();
            logger.LogInformation("Available tools: {Tools}",
                string.Join(", ", availableTools.Select(t => t.Metadata.Name)));

            // Create benchmark runner
            var benchmarks = new FileSystemBenchmarks(
                serviceProvider.GetRequiredService<ILogger<FileSystemBenchmarks>>());

            var agentLogger = serviceProvider.GetRequiredService<ILogger<Agent>>();

            if (useRealLlm)
            {
                // Run with real OpenAI LLM
                var llmFactory = serviceProvider.GetRequiredService<ILlmProviderFactory>();
                var llmProvider = await llmFactory.CreateAvailableProviderAsync();
                logger.LogInformation("Using LLM provider: {Provider}", llmProvider.Name);

                await benchmarks.RunWithRealLlmAsync(
                    llmProvider,
                    toolRegistry,
                    toolExecutor,
                    agentLogger);
            }
            else
            {
                // Run with mocked LLM
                await benchmarks.RunWithMockedLlmAsync(
                    toolRegistry,
                    toolExecutor,
                    agentLogger);
            }

            // Cleanup
            benchmarks.Cleanup();

            logger.LogInformation("\n=== Benchmarks Complete ===");
            logger.LogInformation("All scenarios executed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running benchmarks");
            Environment.Exit(1);
        }
    }
}
