using System.Text.Json.Nodes;
using Andy.Engine.Contracts;
using Andy.Engine.Executor;
using Andy.Engine.Validation;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SimpleAgent;

public static class DirectToolTest
{
    public static async Task TestDirectToolExecution()
    {
        Console.WriteLine("=== Testing Direct Tool Execution ===");

        // Set up basic services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add Andy Tools
        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            options.EnableDetailedTracing = true;
        });

        // Add JSON validator
        services.AddSingleton<IJsonValidator, JsonSchemaValidator>();

        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DirectToolTest");

        // Initialize tools
        var lifecycleManager = serviceProvider.GetRequiredService<IToolLifecycleManager>();
        await lifecycleManager.InitializeAsync();

        var toolRegistry = serviceProvider.GetRequiredService<IToolRegistry>();
        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Test 1: Direct tool execution via Andy.Tools
        Console.WriteLine("\n--- Test 1: Direct Andy.Tools execution ---");
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "now"
        };

        try
        {
            var result = await toolExecutor.ExecuteAsync("datetime_tool", parameters);
            Console.WriteLine($"Direct execution result: Success={result.IsSuccessful}, Data={result.Data}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Direct execution failed: {ex.Message}");
        }

        // Test 2: Andy.Engine ToolAdapter execution
        Console.WriteLine("\n--- Test 2: Andy.Engine ToolAdapter execution ---");
        var validator = serviceProvider.GetRequiredService<IJsonValidator>();
        var toolAdapter = new ToolAdapter(toolRegistry, toolExecutor, validator, loggerFactory.CreateLogger<ToolAdapter>());

        var call = new ToolCall("datetime_tool", JsonNode.Parse("""{"operation": "now"}""")!);

        try
        {
            var result = await toolAdapter.ExecuteAsync(call);
            Console.WriteLine($"ToolAdapter execution result: Success={result.Ok}, Data={result.Data}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ToolAdapter execution failed: {ex.Message}");
        }

        // Test 3: Check tool metadata
        Console.WriteLine("\n--- Test 3: Tool metadata ---");
        var tool = toolRegistry.GetTool("datetime_tool");
        if (tool != null)
        {
            Console.WriteLine($"Tool found: {tool.Metadata.Name}");
            Console.WriteLine("Parameters:");
            foreach (var param in tool.Metadata.Parameters)
            {
                Console.WriteLine($"  {param.Name} ({param.Type}) - Required: {param.Required}");
                if (param.AllowedValues?.Count > 0)
                {
                    Console.WriteLine($"    Allowed values: {string.Join(", ", param.AllowedValues)}");
                }
            }
        }
        else
        {
            Console.WriteLine("Tool not found!");
        }
    }
}