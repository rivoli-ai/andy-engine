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

namespace Andy.Engine.Tests.Benchmarks.Common;

/// <summary>
/// Generic base class for integration tests that execute benchmark scenarios through the Agent.
/// Subclass this to create tool-specific integration test bases.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IToolRegistry ToolRegistry;
    protected readonly IToolExecutor ToolExecutor;
    protected readonly ITestOutputHelper Output;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;

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
            ConfigureToolOptions(options);
        });

        ServiceProvider = services.BuildServiceProvider();

        var lifecycleManager = ServiceProvider.GetRequiredService<IToolLifecycleManager>();
        lifecycleManager.InitializeAsync().GetAwaiter().GetResult();

        ToolRegistry = ServiceProvider.GetRequiredService<IToolRegistry>();
        ToolExecutor = ServiceProvider.GetRequiredService<IToolExecutor>();
    }

    /// <summary>
    /// Override to configure tool options (e.g., AllowedPaths for filesystem tools).
    /// </summary>
    protected virtual void ConfigureToolOptions(ToolFrameworkOptions options)
    {
    }

    /// <summary>
    /// Override to provide a domain-specific system prompt for the agent.
    /// </summary>
    protected virtual string GetSystemPrompt()
    {
        return "You are a helpful assistant with access to various tools. When users ask you to perform operations, use the provided tools to complete the task. After you get the results from a tool, provide a clear text response to the user summarizing what you found or accomplished.";
    }

    /// <summary>
    /// Override to set the working directory for the agent. Defaults to current directory.
    /// </summary>
    protected virtual string GetWorkingDirectory()
    {
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Runs a scenario with the specified LLM mode.
    /// </summary>
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
    /// Runs a scenario with a mocked LLM and validates results.
    /// </summary>
    protected async Task<BenchmarkResult> RunWithMockedLlmAsync(BenchmarkScenario scenario)
    {
        var capturingExecutor = new CapturingToolExecutor(ToolExecutor);

        var llmInteractions = new List<LlmInteraction>();
        var mockedLlm = new MockedLlmProvider(scenario);
        var capturingLlm = new CapturingLlmProvider(mockedLlm, llmInteractions);

        var availableTools = ToolRegistry.Tools.Where(t => t.IsEnabled).Select(t => t.Metadata.Id).ToList();
        Output.WriteLine($"Available tools ({availableTools.Count}): {string.Join(", ", availableTools)}");
        Output.WriteLine($"Using LLM: {mockedLlm.Name} (Mock)");

        var agentLogger = ServiceProvider.GetRequiredService<ILogger<SimpleAgent>>();
        var agent = new SimpleAgent(
            capturingLlm,
            ToolRegistry,
            capturingExecutor,
            systemPrompt: GetSystemPrompt(),
            maxTurns: 10,
            workingDirectory: GetWorkingDirectory(),
            logger: agentLogger
        );

        var runner = new SimpleScenarioRunner(agent, GetWorkingDirectory());
        var result = await runner.RunAsync(scenario);

        MergeCapturedData(result, capturingExecutor, llmInteractions);

        result.Metadata["AgentType"] = agent.GetType().Name;
        result.Metadata["Provider"] = mockedLlm.Name;
        result.Metadata["Model"] = "mocked-model";

        return result;
    }

    /// <summary>
    /// Runs a scenario with real LLM using configuration from appsettings.json.
    /// </summary>
    protected async Task<BenchmarkResult> RunWithRealLlmAsync(BenchmarkScenario scenario)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
        });

        services.AddLlmServices(configuration);
        services.ConfigureLlmFromEnvironment();

        services.AddAndyTools(options =>
        {
            options.RegisterBuiltInTools = true;
            ConfigureToolOptions(options);
        });

        var provider = services.BuildServiceProvider();

        var lifecycleManager = provider.GetRequiredService<IToolLifecycleManager>();
        await lifecycleManager.InitializeAsync();

        var toolRegistry = provider.GetRequiredService<IToolRegistry>();
        var toolExecutor = provider.GetRequiredService<IToolExecutor>();

        var llmFactory = provider.GetRequiredService<ILlmProviderFactory>();

        var defaultProviderName = configuration["Llm:DefaultProvider"];
        if (string.IsNullOrEmpty(defaultProviderName))
        {
            throw new InvalidOperationException("Llm:DefaultProvider not configured in appsettings.json");
        }

        var llmProvider = llmFactory.CreateProvider(defaultProviderName);
        var llmInteractions = new List<LlmInteraction>();
        var capturingLlm = new CapturingLlmProvider(llmProvider, llmInteractions);

        var capturingExecutor = new CapturingToolExecutor(toolExecutor);

        var availableTools = toolRegistry.Tools.Where(t => t.IsEnabled).Select(t => t.Metadata.Id).ToList();
        Output.WriteLine($"Available tools ({availableTools.Count}): {string.Join(", ", availableTools)}");
        Output.WriteLine($"Using LLM: {llmProvider.Name}");

        var agentLogger = provider.GetRequiredService<ILogger<SimpleAgent>>();
        var agent = new SimpleAgent(
            capturingLlm,
            toolRegistry,
            capturingExecutor,
            systemPrompt: GetSystemPrompt(),
            maxTurns: 10,
            workingDirectory: GetWorkingDirectory(),
            logger: agentLogger
        );

        var runner = new SimpleScenarioRunner(agent, GetWorkingDirectory());
        var result = await runner.RunAsync(scenario);

        MergeCapturedData(result, capturingExecutor, llmInteractions);

        result.Metadata["AgentType"] = agent.GetType().Name;
        result.Metadata["Provider"] = llmProvider.Name;

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"Scenario execution failed: {result.ErrorMessage}");
            errorDetails.AppendLine();
            errorDetails.AppendLine("NOTE: Check the test output above for detailed error logs from the agent.");
            if (!string.IsNullOrEmpty(result.StackTrace))
            {
                errorDetails.AppendLine();
                errorDetails.AppendLine("Stack trace:");
                errorDetails.AppendLine(result.StackTrace);
            }
            throw new InvalidOperationException(errorDetails.ToString());
        }

        var firstInteraction = llmInteractions.FirstOrDefault();
        if (firstInteraction == null || string.IsNullOrEmpty(firstInteraction.Model))
        {
            throw new InvalidOperationException(
                $"Model information was not captured from LLM interaction. " +
                $"Interaction count: {llmInteractions.Count}, " +
                $"First interaction: {(firstInteraction == null ? "null" : "exists but model is empty")}.");
        }
        result.Metadata["Model"] = firstInteraction.Model;

        return result;
    }

    /// <summary>
    /// Validates a benchmark result using xUnit assertions.
    /// </summary>
    protected void AssertBenchmarkSuccess(BenchmarkResult result, BenchmarkScenario scenario)
    {
        if (result.ErrorMessage?.Contains("skipping") == true)
        {
            Output.WriteLine($"Test skipped: {result.ErrorMessage}");
            return;
        }

        Output.WriteLine($"Benchmark: {scenario.Id}");

        if (result.Metadata.TryGetValue("AgentType", out var agentType))
            Output.WriteLine($"Agent: {agentType}");
        if (result.Metadata.TryGetValue("Provider", out var providerName))
            Output.WriteLine($"Provider: {providerName}");
        if (result.Metadata.TryGetValue("Model", out var model))
            Output.WriteLine($"Model: {model}");

        Output.WriteLine($"Duration: {result.Duration.TotalMilliseconds:F0}ms");
        Output.WriteLine($"Tool Invocations: {result.ToolInvocations.Count}");

        foreach (var invocation in result.ToolInvocations)
        {
            Output.WriteLine($"  Tool: {invocation.ToolType}");
            if (invocation.Parameters.Count > 0)
            {
                foreach (var param in invocation.Parameters)
                    Output.WriteLine($"    {param.Key}: {param.Value}");
            }

            if (invocation.Result != null)
            {
                string resultStr = invocation.Result is string str
                    ? str
                    : System.Text.Json.JsonSerializer.Serialize(invocation.Result,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

                var preview = resultStr.Length > 300 ? resultStr.Substring(0, 300) + "..." : resultStr;
                Output.WriteLine($"    Result: {preview}");
            }

            Output.WriteLine($"    Success: {invocation.Success}");
            if (!string.IsNullOrEmpty(invocation.ErrorMessage))
                Output.WriteLine($"    Error: {invocation.ErrorMessage}");
        }

        if (result.LlmInteractions.Count > 0)
        {
            Output.WriteLine($"\nLLM Interactions: {result.LlmInteractions.Count}");
            for (int i = 0; i < result.LlmInteractions.Count; i++)
            {
                var interaction = result.LlmInteractions[i];
                var contextSizeKb = interaction.ContextSize / 1024.0;
                var contextSizeDisplay = contextSizeKb >= 1.0
                    ? $"{contextSizeKb:F2} KB"
                    : $"{interaction.ContextSize} chars";

                Output.WriteLine($"  [{i + 1}] Context: {contextSizeDisplay}");

                var requestPreview = interaction.Request.Length > 200
                    ? interaction.Request.Substring(0, 200) + "..."
                    : interaction.Request;
                Output.WriteLine($"      Request: {requestPreview}");

                var responsePreview = interaction.Response.Length > 300
                    ? interaction.Response.Substring(0, 300) + "..."
                    : interaction.Response;
                Output.WriteLine($"      Response: {responsePreview}");

                if (!string.IsNullOrEmpty(interaction.Model))
                    Output.WriteLine($"      Model: {interaction.Model}, Tokens: {interaction.RequestTokens + interaction.ResponseTokens}");
            }
        }

        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");

        var validator = new ToolInvocationValidator();
        var validationResult = validator.ValidateAsync(scenario, result).GetAwaiter().GetResult();

        Output.WriteLine($"Validation Checks:");
        foreach (var expectedTool in scenario.ExpectedTools)
        {
            Output.WriteLine($"  Tool: {expectedTool.Type}");
            Output.WriteLine($"    Expected invocations: {expectedTool.MinInvocations}-{expectedTool.MaxInvocations}");
            if (expectedTool.Parameters.Count > 0)
            {
                Output.WriteLine($"    Expected parameters:");
                foreach (var param in expectedTool.Parameters)
                    Output.WriteLine($"      {param.Key} = {param.Value}");
            }
        }

        if (validationResult.Passed)
        {
            Output.WriteLine($"Validation: PASSED - {validationResult.Message}");
        }
        else
        {
            Output.WriteLine($"Validation: FAILED - {validationResult.Message}");
            foreach (var detail in validationResult.Details)
                Output.WriteLine($"  {detail.Key}: {detail.Value}");
        }

        Assert.True(validationResult.Passed,
            $"Tool invocation validation failed: {validationResult.Message}\n" +
            $"Details: {string.Join(", ", validationResult.Details.Select(kv => $"{kv.Key}={kv.Value}"))}");

        if (scenario.Validation != null)
        {
            ValidateToolResults(result, scenario.Validation);
        }
    }

    private void MergeCapturedData(BenchmarkResult result, CapturingToolExecutor capturingExecutor, List<LlmInteraction> llmInteractions)
    {
        var captured = capturingExecutor.CapturedInvocations;
        var capturedResults = capturingExecutor.CapturedResults;
        for (int i = 0; i < Math.Min(result.ToolInvocations.Count, captured.Count); i++)
        {
            var oldInvocation = result.ToolInvocations[i];
            if (i < capturedResults.Count && capturedResults[i] != null)
            {
                var toolResult = capturedResults[i];
                result.ToolInvocations[i] = new ToolInvocationRecord
                {
                    ToolType = oldInvocation.ToolType,
                    Parameters = captured[i].Parameters,
                    Result = toolResult.Data ?? toolResult.Message,
                    Success = toolResult.IsSuccessful,
                    ErrorMessage = toolResult.ErrorMessage,
                    Timestamp = oldInvocation.Timestamp,
                    Duration = oldInvocation.Duration
                };
            }
            else
            {
                result.ToolInvocations[i].Parameters = captured[i].Parameters;
            }
        }

        foreach (var interaction in llmInteractions)
        {
            result.LlmInteractions.Add(interaction);
        }
    }

    private void ValidateToolResults(BenchmarkResult result, ValidationConfig validation)
    {
        var toolResults = string.Join("\n", result.ToolInvocations
            .Select(t =>
            {
                if (!t.Success && !string.IsNullOrEmpty(t.ErrorMessage))
                    return t.ErrorMessage;

                if (t.Result is string str)
                    return str;

                if (t.Result != null)
                {
                    return System.Text.Json.JsonSerializer.Serialize(t.Result,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }

                return string.Empty;
            })
            .Where(s => !string.IsNullOrEmpty(s)));

        var agentResponse = result.LlmInteractions.LastOrDefault()?.Response ?? string.Empty;
        var allContent = string.IsNullOrEmpty(toolResults) ? agentResponse : $"{toolResults}\n{agentResponse}";

        Output.WriteLine($"\nContent Validation:");

        if (validation.ResponseMustContain.Count > 0)
        {
            var checkLocation = string.IsNullOrEmpty(toolResults) ? "agent response" : "tool results and response";
            Output.WriteLine($"  Checking {validation.ResponseMustContain.Count} required string(s) in {checkLocation}:");
            foreach (var required in validation.ResponseMustContain)
            {
                var found = allContent.Contains(required, StringComparison.OrdinalIgnoreCase);
                Output.WriteLine($"    {(found ? "OK" : "MISSING")} '{required}'");
                Assert.True(found, $"Response must contain '{required}' but it was not found.\nContent checked:\n{allContent}");
            }
        }

        if (validation.ResponseMustContainAny.Count > 0)
        {
            var checkLocation = string.IsNullOrEmpty(toolResults) ? "agent response" : "tool results and response";
            Output.WriteLine($"  Checking at least one of {validation.ResponseMustContainAny.Count} string(s) in {checkLocation}:");
            var foundAny = false;
            foreach (var option in validation.ResponseMustContainAny)
            {
                var found = allContent.Contains(option, StringComparison.OrdinalIgnoreCase);
                Output.WriteLine($"    {(found ? "OK" : "MISSING")} '{option}'");
                if (found) foundAny = true;
            }
            Assert.True(foundAny, $"Response must contain at least one of: {string.Join(", ", validation.ResponseMustContainAny.Select(s => $"'{s}'"))}\nContent checked:\n{allContent}");
        }

        if (validation.ResponseMustNotContain.Count > 0)
        {
            Output.WriteLine($"  Checking {validation.ResponseMustNotContain.Count} forbidden string(s) not in content:");
            foreach (var forbidden in validation.ResponseMustNotContain)
            {
                var found = allContent.Contains(forbidden, StringComparison.OrdinalIgnoreCase);
                Output.WriteLine($"    {(found ? "FOUND (bad)" : "OK")} '{forbidden}'");
                Assert.False(found, $"Response must NOT contain '{forbidden}' but it was found.\nContent checked:\n{allContent}");
            }
        }

        if (validation.MinResponseLength.HasValue)
        {
            var actualLength = allContent.Length;
            var meetsRequirement = actualLength >= validation.MinResponseLength.Value;
            Output.WriteLine($"  Response length: {actualLength} chars {(meetsRequirement ? "OK" : "TOO SHORT")} (min: {validation.MinResponseLength.Value})");
            Assert.True(meetsRequirement,
                $"Tool results length ({actualLength}) is less than minimum required ({validation.MinResponseLength.Value})");
        }

        Output.WriteLine($"Content validation: PASSED");
    }

    public virtual void Dispose()
    {
    }

    /// <summary>
    /// Wraps an LLM provider to capture interactions for benchmarking.
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

            var modelFromRequest = request.Config?.Model;
            var modelFromResponse = response.Model;
            string capturedModel;
            if (!string.IsNullOrEmpty(modelFromRequest))
                capturedModel = modelFromRequest;
            else if (!string.IsNullOrEmpty(modelFromResponse))
                capturedModel = modelFromResponse;
            else if (_inner.Name == "MockedLLM")
                capturedModel = "mocked-model";
            else
                capturedModel = _inner.Name ?? "unknown";

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
    /// Mocked LLM provider that returns predetermined tool calls from scenario expectations.
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
            => Task.FromResult(true);

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

            if (_callCount <= _scenario.ExpectedTools.Count)
            {
                var expectedTool = _scenario.ExpectedTools[_callCount - 1];

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
