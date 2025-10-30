using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Planner;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Andy.Llm.Extensions;
using Andy.Llm.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Base class for file system integration tests that execute scenarios through the Agent
/// </summary>
public abstract class FileSystemIntegrationTestBase : FileSystemTestBase
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IToolRegistry ToolRegistry;
    protected readonly IToolExecutor ToolExecutor;
    protected readonly ITestOutputHelper Output;

    protected FileSystemIntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;

        // Set up service provider with tools
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
        });

        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            options.EnableDetailedTracing = false;
            // Configure permissions to allow access to test directory
            options.DefaultPermissions.AllowedPaths = new HashSet<string> { TestDirectory };
        });

        ServiceProvider = services.BuildServiceProvider();

        // Initialize tools
        var lifecycleManager = ServiceProvider.GetRequiredService<IToolLifecycleManager>();
        lifecycleManager.InitializeAsync().GetAwaiter().GetResult();

        ToolRegistry = ServiceProvider.GetRequiredService<IToolRegistry>();
        ToolExecutor = ServiceProvider.GetRequiredService<IToolExecutor>();
    }

    /// <summary>
    /// Runs a scenario with the specified LLM mode
    /// </summary>
    /// <param name="scenario">The benchmark scenario to run</param>
    /// <param name="mode">The LLM mode to use (Mock or Real)</param>
    /// <returns>Benchmark result</returns>
    protected async Task<BenchmarkResult> RunAsync(BenchmarkScenario scenario, LlmMode mode)
    {
        return mode switch
        {
            LlmMode.Mock => await RunWithMockedLlmAsync(scenario),
            LlmMode.Real => await RunWithRealLlmAsync(scenario),
            _ => throw new ArgumentException($"Unknown LLM mode: {mode}", nameof(mode))
        };
    }

    /// <summary>
    /// Runs a scenario with a mocked LLM and validates results using xUnit assertions
    /// </summary>
    protected async Task<BenchmarkResult> RunWithMockedLlmAsync(BenchmarkScenario scenario)
    {
        // Wrap executor to capture parameters
        var capturingExecutor = new CapturingToolExecutor(ToolExecutor);

        // Create mocked LLM and wrap it to capture interactions
        var llmInteractions = new List<LlmInteraction>();
        var mockedLlm = new MockedLlmProvider(scenario);
        var capturingLlm = new CapturingLlmProvider(mockedLlm, llmInteractions);

        // Log available tools for debugging
        var availableTools = ToolRegistry.Tools.Where(t => t.IsEnabled).Select(t => t.Metadata.Id).ToList();
        Output.WriteLine($"🔧 Available tools ({availableTools.Count}): {string.Join(", ", availableTools)}");
        Output.WriteLine($"🧠 Using LLM: {mockedLlm.Name} (Mock)");

        // Build SimpleAgent
        var agentLogger = ServiceProvider.GetRequiredService<ILogger<SimpleAgent>>();
        var agent = new SimpleAgent(
            capturingLlm,
            ToolRegistry,
            capturingExecutor,
            systemPrompt: "You are a file system assistant. You have access to tools for file operations. When users ask you to perform file operations, use the provided tools to complete the task. Always use tools when available rather than explaining how to use command-line tools. IMPORTANT: Only access the specific directory requested by the user - do not try to access parent directories or other paths without explicit permission. After you get the results from a tool, provide a clear text response to the user summarizing what you found or accomplished.",
            maxTurns: 10,
            workingDirectory: TestDirectory,
            logger: agentLogger
        );

        // Run scenario
        var runner = new SimpleScenarioRunner(agent, TestDirectory);
        var result = await runner.RunAsync(scenario);

        // Merge captured parameters and results
        var captured = capturingExecutor.CapturedInvocations;
        var capturedResults = capturingExecutor.CapturedResults;
        for (int i = 0; i < Math.Min(result.ToolInvocations.Count, captured.Count); i++)
        {
            var oldInvocation = result.ToolInvocations[i];
            // Merge tool result if available
            if (i < capturedResults.Count && capturedResults[i] != null)
            {
                // Store the actual tool output
                var toolResult = capturedResults[i];
                // Create new ToolInvocationRecord with captured data
                result.ToolInvocations[i] = new ToolInvocationRecord
                {
                    ToolType = oldInvocation.ToolType,
                    Parameters = captured[i].Parameters,
                    Result = toolResult.Data ?? toolResult.Message, // Use Data (actual result) if available
                    Success = toolResult.IsSuccessful,
                    ErrorMessage = toolResult.ErrorMessage,
                    Timestamp = oldInvocation.Timestamp,
                    Duration = oldInvocation.Duration
                };
            }
            else
            {
                // Just update parameters
                result.ToolInvocations[i].Parameters = captured[i].Parameters;
            }
        }

        // Add captured LLM interactions
        foreach (var interaction in llmInteractions)
        {
            result.LlmInteractions.Add(interaction);
        }

        // Add agent and provider metadata
        result.Metadata["AgentType"] = agent.GetType().Name;
        result.Metadata["Provider"] = mockedLlm.Name;
        result.Metadata["Model"] = "mocked-model";

        return result;
    }

    /// <summary>
    /// Runs a scenario with real LLM using configuration from appsettings.json
    /// </summary>
    protected async Task<BenchmarkResult> RunWithRealLlmAsync(BenchmarkScenario scenario)
    {
        // Set up services
        var services = new ServiceCollection();

        // Load configuration from appsettings.json
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
        });

        // Load LLM configuration from appsettings.json, then merge environment variables
        services.AddLlmServices(configuration);
        services.ConfigureLlmFromEnvironment();

        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            // Configure permissions to allow access to test directory
            options.DefaultPermissions.AllowedPaths = new HashSet<string> { TestDirectory };
        });

        var provider = services.BuildServiceProvider();

        // Initialize tools
        var lifecycleManager = provider.GetRequiredService<IToolLifecycleManager>();
        await lifecycleManager.InitializeAsync();

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        var toolExecutor = provider.GetRequiredService<IToolExecutor>();

        // Get LLM provider from configuration
        var llmFactory = provider.GetRequiredService<ILlmProviderFactory>();

        // Use the provider specified in DefaultProvider (e.g., "openai/latest-small")
        var defaultProviderName = configuration["Llm:DefaultProvider"];
        if (string.IsNullOrEmpty(defaultProviderName))
        {
            throw new InvalidOperationException("Llm:DefaultProvider not configured in appsettings.json");
        }

        var llmProvider = llmFactory.CreateProvider(defaultProviderName);
        var llmInteractions = new List<LlmInteraction>();
        var capturingLlm = new CapturingLlmProvider(llmProvider, llmInteractions);

        // Wrap executor to capture parameters
        var capturingExecutor = new CapturingToolExecutor(toolExecutor);

        // Log available tools for debugging
        var availableTools = toolRegistry.Tools.Where(t => t.IsEnabled).Select(t => t.Metadata.Id).ToList();
        Output.WriteLine($"🔧 Available tools ({availableTools.Count}): {string.Join(", ", availableTools)}");
        Output.WriteLine($"🧠 Using LLM: {llmProvider.Name}");

        // Build SimpleAgent
        var agentLogger = provider.GetRequiredService<ILogger<SimpleAgent>>();
        var agent = new SimpleAgent(
            capturingLlm,
            toolRegistry,
            capturingExecutor,
            systemPrompt: "You are a file system assistant. You have access to tools for file operations. When users ask you to perform file operations, use the provided tools to complete the task. Always use tools when available rather than explaining how to use command-line tools. IMPORTANT: Only access the specific directory requested by the user - do not try to access parent directories or other paths without explicit permission. After you get the results from a tool, provide a clear text response to the user summarizing what you found or accomplished.",
            maxTurns: 10,
            workingDirectory: TestDirectory,
            logger: agentLogger
        );

        // Run scenario
        var runner = new SimpleScenarioRunner(agent, TestDirectory);
        var result = await runner.RunAsync(scenario);

        // Merge captured parameters and results
        var captured = capturingExecutor.CapturedInvocations;
        var capturedResults = capturingExecutor.CapturedResults;
        for (int i = 0; i < Math.Min(result.ToolInvocations.Count, captured.Count); i++)
        {
            var oldInvocation = result.ToolInvocations[i];
            // Merge tool result if available
            if (i < capturedResults.Count && capturedResults[i] != null)
            {
                // Store the actual tool output
                var toolResult = capturedResults[i];
                // Create new ToolInvocationRecord with captured data
                result.ToolInvocations[i] = new ToolInvocationRecord
                {
                    ToolType = oldInvocation.ToolType,
                    Parameters = captured[i].Parameters,
                    Result = toolResult.Data ?? toolResult.Message, // Use Data (actual result) if available
                    Success = toolResult.IsSuccessful,
                    ErrorMessage = toolResult.ErrorMessage,
                    Timestamp = oldInvocation.Timestamp,
                    Duration = oldInvocation.Duration
                };
            }
            else
            {
                // Just update parameters
                result.ToolInvocations[i].Parameters = captured[i].Parameters;
            }
        }

        // Add captured LLM interactions
        foreach (var interaction in llmInteractions)
        {
            result.LlmInteractions.Add(interaction);
        }

        // Add agent and provider metadata
        result.Metadata["AgentType"] = agent.GetType().Name;
        result.Metadata["Provider"] = llmProvider.Name;

        // Check if the scenario failed before validating interactions
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            // Scenario failed - throw the actual error instead of complaining about missing interactions
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"Scenario execution failed: {result.ErrorMessage}");
            errorDetails.AppendLine();
            errorDetails.AppendLine("NOTE: Check the test output above for detailed error logs from the agent.");
            errorDetails.AppendLine("The agent logs the full exception details before returning the error status.");
            if (!string.IsNullOrEmpty(result.StackTrace))
            {
                errorDetails.AppendLine();
                errorDetails.AppendLine("Stack trace:");
                errorDetails.AppendLine(result.StackTrace);
            }
            throw new InvalidOperationException(errorDetails.ToString());
        }

        // Get model from first interaction - throw if not captured
        var firstInteraction = llmInteractions.FirstOrDefault();
        if (firstInteraction == null || string.IsNullOrEmpty(firstInteraction.Model))
        {
            throw new InvalidOperationException(
                $"Model information was not captured from LLM interaction. " +
                $"Interaction count: {llmInteractions.Count}, " +
                $"First interaction: {(firstInteraction == null ? "null" : "exists but model is empty")}. " +
                $"This usually indicates the LLM call succeeded but didn't return model information.");
        }
        result.Metadata["Model"] = firstInteraction.Model;

        return result;
    }

    /// <summary>
    /// Validates a benchmark result using xUnit assertions
    /// </summary>
    protected void AssertBenchmarkSuccess(BenchmarkResult result, BenchmarkScenario scenario)
    {
        // Skip if test was skipped (e.g., no API key for real LLM)
        if (result.ErrorMessage?.Contains("skipping") == true)
        {
            Output.WriteLine($"⚠️  Test skipped: {result.ErrorMessage}");
            return; // Don't fail the test, just skip validation
        }

        // Log benchmark execution details
        Output.WriteLine($"📊 Benchmark: {scenario.Id}");

        // Log agent and provider information
        if (result.Metadata.TryGetValue("AgentType", out var agentType))
        {
            Output.WriteLine($"🤖 Agent: {agentType}");
        }
        if (result.Metadata.TryGetValue("Provider", out var provider))
        {
            Output.WriteLine($"🔌 Provider: {provider}");
        }
        if (result.Metadata.TryGetValue("Model", out var model))
        {
            Output.WriteLine($"🧠 Model: {model}");
        }

        Output.WriteLine($"⏱️  Duration: {result.Duration.TotalMilliseconds:F0}ms");
        Output.WriteLine($"🔧 Tool Invocations: {result.ToolInvocations.Count}");

        // Log each tool invocation with results
        foreach (var invocation in result.ToolInvocations)
        {
            Output.WriteLine($"  └─ {invocation.ToolType}");
            if (invocation.Parameters.Count > 0)
            {
                foreach (var param in invocation.Parameters)
                {
                    Output.WriteLine($"     • {param.Key}: {param.Value}");
                }
            }

            // Log tool result with actual content
            if (invocation.Result != null)
            {
                string resultStr;
                if (invocation.Result is string str)
                {
                    resultStr = str;
                }
                else
                {
                    // Serialize to JSON for structured data
                    resultStr = System.Text.Json.JsonSerializer.Serialize(invocation.Result,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                }

                // Show preview (limit to 300 chars)
                var preview = resultStr.Length > 300 ? resultStr.Substring(0, 300) + "..." : resultStr;
                Output.WriteLine($"     ✓ Result: {preview}");
            }

            Output.WriteLine($"     ✓ Success: {invocation.Success}");
            if (!string.IsNullOrEmpty(invocation.ErrorMessage))
            {
                Output.WriteLine($"     ✗ Error: {invocation.ErrorMessage}");
            }
        }

        // Log LLM interactions
        if (result.LlmInteractions.Count > 0)
        {
            Output.WriteLine($"\n💬 LLM Interactions: {result.LlmInteractions.Count}");
            for (int i = 0; i < result.LlmInteractions.Count; i++)
            {
                var interaction = result.LlmInteractions[i];

                // Determine interaction type based on request content
                string interactionType;
                if (interaction.Request.Contains("You are a planning agent", StringComparison.OrdinalIgnoreCase))
                {
                    interactionType = "🎯 Planning";
                }
                else if (interaction.Request.Contains("You are a critic", StringComparison.OrdinalIgnoreCase))
                {
                    interactionType = "🔍 Critic";
                }
                else
                {
                    interactionType = "❓ Unknown";
                }

                // Show context size
                var contextSizeKb = interaction.ContextSize / 1024.0;
                var contextSizeDisplay = contextSizeKb >= 1.0
                    ? $"{contextSizeKb:F2} KB"
                    : $"{interaction.ContextSize} chars";

                Output.WriteLine($"  [{i + 1}] {interactionType} - Request (Context: {contextSizeDisplay}):");
                var requestPreview = interaction.Request.Length > 200
                    ? interaction.Request.Substring(0, 200) + "..."
                    : interaction.Request;
                Output.WriteLine($"      {requestPreview}");

                Output.WriteLine($"  [{i + 1}] {interactionType} - Response:");
                var responsePreview = interaction.Response.Length > 300
                    ? interaction.Response.Substring(0, 300) + "..."
                    : interaction.Response;
                Output.WriteLine($"      {responsePreview}");

                if (!string.IsNullOrEmpty(interaction.Model))
                {
                    Output.WriteLine($"      Model: {interaction.Model}, Tokens: {interaction.RequestTokens + interaction.ResponseTokens}");
                }
            }
        }

        // Assert overall success
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");

        // Validate tool invocations using the validator
        var validator = new ToolInvocationValidator();
        var validationResult = validator.ValidateAsync(scenario, result).GetAwaiter().GetResult();

        // Log validation checks performed
        Output.WriteLine($"🔍 Validation Checks:");
        foreach (var expectedTool in scenario.ExpectedTools)
        {
            Output.WriteLine($"  • Tool: {expectedTool.Type}");
            Output.WriteLine($"    - Expected invocations: {expectedTool.MinInvocations}-{expectedTool.MaxInvocations}");
            if (expectedTool.Parameters.Count > 0)
            {
                Output.WriteLine($"    - Expected parameters:");
                foreach (var param in expectedTool.Parameters)
                {
                    Output.WriteLine($"      ✓ {param.Key} = {param.Value}");
                }
            }
        }

        // Log validation result
        if (validationResult.Passed)
        {
            Output.WriteLine($"✅ Validation: PASSED - {validationResult.Message}");
            if (validationResult.Details.Count > 0)
            {
                foreach (var detail in validationResult.Details)
                {
                    Output.WriteLine($"   • {detail.Key}: {detail.Value}");
                }
            }
        }
        else
        {
            Output.WriteLine($"❌ Validation: FAILED - {validationResult.Message}");
            foreach (var detail in validationResult.Details)
            {
                Output.WriteLine($"   • {detail.Key}: {detail.Value}");
            }
        }

        Assert.True(validationResult.Passed,
            $"Tool invocation validation failed: {validationResult.Message}\n" +
            $"Details: {string.Join(", ", validationResult.Details.Select(kv => $"{kv.Key}={kv.Value}"))}");

        // Validate tool results contain expected content
        if (scenario.Validation != null)
        {
            ValidateToolResults(result, scenario.Validation);
        }
    }

    /// <summary>
    /// Builds a string representation of the file structure in the test directory
    /// </summary>
    private string BuildFileStructureContext(string rootPath)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Working directory: {rootPath}");

        try
        {
            BuildFileTree(rootPath, rootPath, sb, "");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error reading file structure: {ex.Message}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively builds file tree representation
    /// </summary>
    private void BuildFileTree(string rootPath, string currentPath, System.Text.StringBuilder sb, string indent)
    {
        try
        {
            var entries = Directory.GetFileSystemEntries(currentPath)
                .OrderBy(e => Directory.Exists(e) ? 0 : 1) // Directories first
                .ThenBy(e => Path.GetFileName(e));

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var relativePath = Path.GetRelativePath(rootPath, entry);

                if (Directory.Exists(entry))
                {
                    sb.AppendLine($"{indent}📁 {relativePath}/");
                    BuildFileTree(rootPath, entry, sb, indent + "  ");
                }
                else
                {
                    var fileInfo = new FileInfo(entry);
                    sb.AppendLine($"{indent}📄 {relativePath} ({fileInfo.Length} bytes)");
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
    }

    /// <summary>
    /// Validates that tool results contain expected strings and patterns
    /// </summary>
    private void ValidateToolResults(BenchmarkResult result, ValidationConfig validation)
    {
        // Collect all tool results as strings, serializing complex objects to JSON
        var allResults = string.Join("\n", result.ToolInvocations
            .Where(t => t.Result != null)
            .Select(t =>
            {
                if (t.Result is string str)
                    return str;
                // Serialize complex objects to JSON for content checking
                return System.Text.Json.JsonSerializer.Serialize(t.Result,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }));

        Output.WriteLine($"\n🔍 Content Validation:");

        // Check ResponseMustContain
        if (validation.ResponseMustContain.Count > 0)
        {
            Output.WriteLine($"  • Checking {validation.ResponseMustContain.Count} required string(s) in tool results:");
            foreach (var required in validation.ResponseMustContain)
            {
                var found = allResults.Contains(required, StringComparison.OrdinalIgnoreCase);
                Output.WriteLine($"    {(found ? "✓" : "✗")} '{required}' {(found ? "found" : "NOT FOUND")}");
                Assert.True(found, $"Tool results must contain '{required}' but it was not found.\nTool results:\n{allResults}");
            }
        }

        // Check ResponseMustNotContain
        if (validation.ResponseMustNotContain.Count > 0)
        {
            Output.WriteLine($"  • Checking {validation.ResponseMustNotContain.Count} forbidden string(s) not in tool results:");
            foreach (var forbidden in validation.ResponseMustNotContain)
            {
                var found = allResults.Contains(forbidden, StringComparison.OrdinalIgnoreCase);
                Output.WriteLine($"    {(found ? "✗" : "✓")} '{forbidden}' {(found ? "FOUND (should not be present)" : "not found (correct)")}");
                Assert.False(found, $"Tool results must NOT contain '{forbidden}' but it was found.\nTool results:\n{allResults}");
            }
        }

        // Check minimum response length
        if (validation.MinResponseLength.HasValue)
        {
            var actualLength = allResults.Length;
            var meetsRequirement = actualLength >= validation.MinResponseLength.Value;
            Output.WriteLine($"  • Response length: {actualLength} chars {(meetsRequirement ? "✓" : "✗")} (min: {validation.MinResponseLength.Value})");
            Assert.True(meetsRequirement,
                $"Tool results length ({actualLength}) is less than minimum required ({validation.MinResponseLength.Value})");
        }

        Output.WriteLine($"✅ Content validation: PASSED");
    }

    /// <summary>
    /// Wraps an LLM provider to capture interactions for benchmarking
    /// </summary>
    private class CapturingLlmProvider : ILlmProvider
    {
        private readonly ILlmProvider _inner;
        private readonly List<LlmInteraction> _interactions;

        public CapturingLlmProvider(ILlmProvider inner, List<LlmInteraction> interactions)
        {
            _inner = inner;
            _interactions = interactions;
        }

        public string Name => _inner.Name;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => _inner.IsAvailableAsync(cancellationToken);

        public Task<IEnumerable<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => _inner.ListModelsAsync(cancellationToken);

        public IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
            => _inner.StreamCompleteAsync(request, cancellationToken);

        public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            var requestText = string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}"));

            LlmResponse response;
            try
            {
                response = await _inner.CompleteAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                // If the LLM call fails, capture the error and re-throw
                _interactions.Add(new LlmInteraction
                {
                    Request = requestText,
                    Response = $"ERROR: {ex.Message}",
                    Timestamp = DateTime.UtcNow,
                    RequestTokens = 0,
                    ResponseTokens = 0,
                    Model = request.Config?.Model ?? _inner.Name ?? "unknown",
                    ContextSize = requestText.Length
                });
                throw;
            }

            // Capture model from request config or response
            var modelFromRequest = request.Config?.Model;
            var modelFromResponse = response.Model;
            string capturedModel;
            if (!string.IsNullOrEmpty(modelFromRequest))
            {
                capturedModel = modelFromRequest;
            }
            else if (!string.IsNullOrEmpty(modelFromResponse))
            {
                capturedModel = modelFromResponse;
            }
            else if (_inner.Name == "MockedLLM")
            {
                // For mocked LLM, use placeholder model name
                capturedModel = "mocked-model";
            }
            else
            {
                // For real LLMs, use provider name as fallback
                capturedModel = _inner.Name ?? "unknown";
            }

            _interactions.Add(new LlmInteraction
            {
                Request = requestText,
                Response = response.AssistantMessage?.Content ?? "",
                Timestamp = DateTime.UtcNow,
                RequestTokens = response.Usage?.PromptTokens ?? 0,
                ResponseTokens = response.Usage?.CompletionTokens ?? 0,
                Model = capturedModel,
                ContextSize = requestText.Length
            });

            return response;
        }
    }

    /// <summary>
    /// Mocked LLM provider for testing
    /// </summary>
    private class MockedLlmProvider : ILlmProvider
    {
        private readonly BenchmarkScenario _scenario;
        private int _callCount = 0;

        public MockedLlmProvider(BenchmarkScenario scenario)
        {
            _scenario = scenario;
        }

        public string Name => "MockedLLM";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<IEnumerable<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
        {
            var models = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Id = "mocked-model",
                    Name = "Mocked Model",
                    Provider = "Mock",
                    SupportsFunctions = true
                }
            };
            return Task.FromResult<IEnumerable<ModelInfo>>(models);
        }

        public IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Streaming not supported in mocked provider");
        }

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            _callCount++;

            // For each expected tool, return tool calls
            if (_callCount <= _scenario.ExpectedTools.Count)
            {
                var expectedTool = _scenario.ExpectedTools[_callCount - 1];

                // Create tool call with arguments as JSON
                var argsJson = System.Text.Json.JsonSerializer.Serialize(expectedTool.Parameters);

                var toolCall = new ToolCall
                {
                    Id = $"call_{_callCount}",
                    Name = expectedTool.Type,
                    ArgumentsJson = argsJson
                };

                return Task.FromResult(new LlmResponse
                {
                    AssistantMessage = new Message
                    {
                        Role = MessageRole.Assistant,
                        Content = "",
                        ToolCalls = new List<ToolCall> { toolCall }
                    },
                    Usage = new LlmUsage
                    {
                        PromptTokens = 100,
                        CompletionTokens = 50,
                        TotalTokens = 150
                    }
                });
            }

            // After all tools, return text response
            return Task.FromResult(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = "Task completed successfully."
                },
                FinishReason = "stop",
                Usage = new LlmUsage
                {
                    PromptTokens = 100,
                    CompletionTokens = 20,
                    TotalTokens = 120
                }
            });
        }
    }
}
