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
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
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

        // Build SimpleAgent
        var agentLogger = ServiceProvider.GetRequiredService<ILogger<SimpleAgent>>();
        var agent = new SimpleAgent(
            capturingLlm,
            ToolRegistry,
            capturingExecutor,
            systemPrompt: "You are a file system tool agent that helps users with file operations.",
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
    /// Runs a scenario with real OpenAI LLM (requires OPENAI_API_KEY)
    /// </summary>
    protected async Task<BenchmarkResult> RunWithRealLlmAsync(BenchmarkScenario scenario)
    {
        // Skip if no API key
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return new BenchmarkResult
            {
                ScenarioId = scenario.Id,
                Success = false,
                Duration = TimeSpan.Zero,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                ToolInvocations = new List<ToolInvocationRecord>(),
                LlmInteractions = new List<LlmInteraction>(),
                ValidationResults = new List<ValidationResult>(),
                Metrics = new PerformanceMetrics(),
                ErrorMessage = "OPENAI_API_KEY not set - skipping real LLM test"
            };
        }

        // Set up LLM services with configuration from appsettings.json
        var services = new ServiceCollection();

        // Add configuration from appsettings.json
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
        });

        // Configure LLM from environment variables (following andy-llm examples pattern)
        services.ConfigureLlmFromEnvironment();
        services.AddLlmServices(options =>
        {
            // Set OpenAI as default provider
            options.DefaultProvider = "openai";
        });

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

        // Get LLM and wrap to capture interactions
        var llmFactory = provider.GetRequiredService<ILlmProviderFactory>();
        var llmProvider = await llmFactory.CreateAvailableProviderAsync();
        Console.WriteLine($"[DEBUG] Selected LLM provider: {llmProvider.Name}");
        var llmInteractions = new List<LlmInteraction>();
        var capturingLlm = new CapturingLlmProvider(llmProvider, llmInteractions);

        // Wrap executor to capture parameters
        var capturingExecutor = new CapturingToolExecutor(toolExecutor);

        // Build SimpleAgent
        var agentLogger = provider.GetRequiredService<ILogger<SimpleAgent>>();
        var agent = new SimpleAgent(
            capturingLlm,
            toolRegistry,
            capturingExecutor,
            systemPrompt: "You are a file system tool agent that helps users with file operations.",
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
        // Get model from first interaction if available
        var model = llmInteractions.FirstOrDefault()?.Model ?? "unknown";
        result.Metadata["Model"] = model;

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
            var response = await _inner.CompleteAsync(request, cancellationToken);

            _interactions.Add(new LlmInteraction
            {
                Request = requestText,
                Response = response.AssistantMessage?.Content ?? "",
                Timestamp = DateTime.UtcNow,
                RequestTokens = response.Usage?.PromptTokens ?? 0,
                ResponseTokens = response.Usage?.CompletionTokens ?? 0,
                Model = request.Config?.Model ?? response.Model ?? "unknown",
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
