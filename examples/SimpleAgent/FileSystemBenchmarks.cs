using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Andy.Engine.Contracts;
using Andy.Engine.Planner;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools.Core;
using Andy.Tools.Framework;
using Andy.Llm.Services;
using Microsoft.Extensions.Logging;
using Andy.Tools.Execution;

namespace SimpleAgent;

/// <summary>
/// Runs file system benchmarks with both mocked and real LLM
/// </summary>
public class FileSystemBenchmarks
{
    private readonly ILogger _logger;
    private readonly string _testDirectory;

    public FileSystemBenchmarks(ILogger logger)
    {
        _logger = logger;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"fs_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    /// Run benchmarks with a mocked LLM that returns pre-determined responses
    /// </summary>
    public async Task RunWithMockedLlmAsync(
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        ILogger<Agent> agentLogger)
    {
        _logger.LogInformation("=== Running File System Benchmarks with MOCKED LLM ===");

        // Create test file structure
        CreateTestFileStructure();

        // Create scenarios
        var scenarios = CreateListDirectoryScenarios();

        foreach (var scenario in scenarios)
        {
            _logger.LogInformation("\n--- Running Scenario: {ScenarioId} ---", scenario.Id);
            _logger.LogInformation("Description: {Description}", scenario.Description);

            // Wrap the tool executor to capture parameters
            var capturingExecutor = new CapturingToolExecutor(toolExecutor);

            // Create mocked LLM that responds with expected tool calls
            var mockedLlm = new MockedLlmProvider(scenario, _logger);

            // Build agent with mocked LLM and capturing executor
            var agent = AgentBuilder.Create()
                .WithDefaults(mockedLlm, toolRegistry, capturingExecutor)
                .WithPlannerOptions(new PlannerOptions
                {
                    Temperature = 0,
                    MaxTokens = 1000,
                    SystemPrompt = "You are a file system tool agent."
                })
                .WithLogger(agentLogger)
                .Build();

            // Run scenario
            var runner = new ScenarioRunner(agent, _testDirectory);
            var result = await runner.RunAsync(scenario);

            // Merge captured parameters into tool invocations
            var captured = capturingExecutor.CapturedInvocations;
            for (int i = 0; i < Math.Min(result.ToolInvocations.Count, captured.Count); i++)
            {
                result.ToolInvocations[i].Parameters = captured[i].Parameters;
            }

            // Validate results
            LogBenchmarkResult(result, scenario);

            // Validate with tool invocation validator
            var validator = new ToolInvocationValidator();
            var validationResult = await validator.ValidateAsync(scenario, result);

            _logger.LogInformation("Validation: {Status} - {Message}",
                validationResult.Passed ? "PASSED" : "FAILED",
                validationResult.Message);
        }
    }

    /// <summary>
    /// Run benchmarks with real OpenAI LLM
    /// </summary>
    public async Task RunWithRealLlmAsync(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        ILogger<Agent> agentLogger)
    {
        _logger.LogInformation("=== Running File System Benchmarks with REAL LLM (OpenAI) ===");

        // Create test file structure
        CreateTestFileStructure();

        // Create scenarios
        var scenarios = CreateListDirectoryScenarios();

        foreach (var scenario in scenarios)
        {
            _logger.LogInformation("\n--- Running Scenario: {ScenarioId} ---", scenario.Id);
            _logger.LogInformation("Description: {Description}", scenario.Description);
            _logger.LogInformation("Prompt: {Prompt}", scenario.Context.Prompts.First());

            // Wrap the tool executor to capture parameters
            var capturingExecutor = new CapturingToolExecutor(toolExecutor);

            // Build agent with real LLM and capturing executor
            var agent = AgentBuilder.Create()
                .WithDefaults(llmProvider, toolRegistry, capturingExecutor)
                .WithPlannerOptions(new PlannerOptions
                {
                    Temperature = 0,
                    MaxTokens = 1000,
                    SystemPrompt = """
                        You are a JSON-only file system agent. Respond ONLY with valid JSON.

                        Output format (choose one):
                        {"action": "call_tool", "name": "tool_id", "args": {...}}
                        {"action": "stop", "reason": "completed"}

                        Rules:
                        - ONLY output JSON, nothing else
                        - Use exact tool IDs from the available tools list
                        - Use exact parameter names as specified
                        - For list_directory, use directory_path parameter
                        - For read_file, use file_path parameter
                        """
                })
                .WithLogger(agentLogger)
                .Build();

            // Run scenario
            var runner = new ScenarioRunner(agent, _testDirectory);
            var result = await runner.RunAsync(scenario);

            // Merge captured parameters into tool invocations
            var captured = capturingExecutor.CapturedInvocations;
            for (int i = 0; i < Math.Min(result.ToolInvocations.Count, captured.Count); i++)
            {
                result.ToolInvocations[i].Parameters = captured[i].Parameters;
            }

            // Validate results
            LogBenchmarkResult(result, scenario);

            // Validate with tool invocation validator
            var validator = new ToolInvocationValidator();
            var validationResult = await validator.ValidateAsync(scenario, result);

            _logger.LogInformation("Validation: {Status} - {Message}",
                validationResult.Passed ? "PASSED" : "FAILED",
                validationResult.Message);

            if (!validationResult.Passed)
            {
                _logger.LogWarning("Validation details: {Details}",
                    string.Join(", ", validationResult.Details.Select(kv => $"{kv.Key}={kv.Value}")));
            }
        }
    }

    private void CreateTestFileStructure()
    {
        // Clean up if exists
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        Directory.CreateDirectory(_testDirectory);

        // Create sample files and directories
        File.WriteAllText(Path.Combine(_testDirectory, "readme.txt"), "This is a readme file.\nIt has multiple lines.\nLine 3 here.");
        File.WriteAllText(Path.Combine(_testDirectory, "data.json"), "{\"name\": \"test\", \"value\": 42}");
        File.WriteAllText(Path.Combine(_testDirectory, "script.sh"), "#!/bin/bash\necho 'Hello World'");

        // Create subdirectories with files
        var docsDir = Path.Combine(_testDirectory, "documents");
        Directory.CreateDirectory(docsDir);
        File.WriteAllText(Path.Combine(docsDir, "report.txt"), "Annual Report 2024\n\nSection 1: Overview");
        File.WriteAllText(Path.Combine(docsDir, "notes.md"), "# Notes\n\n- Item 1\n- Item 2");

        var sourceDir = Path.Combine(_testDirectory, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "program.cs"), "using System;\n\nclass Program\n{\n    static void Main() { }\n}");

        // Hidden file (Unix-style)
        File.WriteAllText(Path.Combine(_testDirectory, ".hidden"), "Hidden file content");

        _logger.LogInformation("Test directory created: {Directory}", _testDirectory);
    }

    private List<BenchmarkScenario> CreateListDirectoryScenarios()
    {
        // Use scenarios from the centralized scenario definitions
        return ListDirectoryScenarios.CreateScenarios(_testDirectory);
    }

    private void LogBenchmarkResult(BenchmarkResult result, BenchmarkScenario scenario)
    {
        _logger.LogInformation("Result: {Status}", result.Success ? "SUCCESS" : "FAILED");
        _logger.LogInformation("Duration: {Duration:F2}s", result.Duration.TotalSeconds);
        _logger.LogInformation("Tool Invocations: {Count}", result.ToolInvocations.Count);

        foreach (var invocation in result.ToolInvocations)
        {
            _logger.LogInformation("  - {ToolType} at {Timestamp}",
                invocation.ToolType,
                invocation.Timestamp.ToString("HH:mm:ss.fff"));
        }

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            _logger.LogError("Error: {ErrorMessage}", result.ErrorMessage);
        }
    }

    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
                _logger.LogInformation("Cleaned up test directory: {Directory}", _testDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup test directory");
        }
    }
}

/// <summary>
/// Mocked LLM provider that returns predetermined responses based on the scenario
/// </summary>
public class MockedLlmProvider : ILlmProvider
{
    private readonly BenchmarkScenario _scenario;
    private readonly ILogger _logger;
    private int _callCount = 0;

    public MockedLlmProvider(BenchmarkScenario scenario, ILogger logger)
    {
        _scenario = scenario;
        _logger = logger;
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
        _logger.LogInformation("MockedLLM Call #{CallCount}", _callCount);

        // For each expected tool in the scenario, return a call_tool action
        if (_callCount <= _scenario.ExpectedTools.Count)
        {
            var expectedTool = _scenario.ExpectedTools[_callCount - 1];
            var parameters = expectedTool.Parameters;

            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "call_tool",
                name = expectedTool.Type,
                args = parameters,
                reason = $"Calling {expectedTool.Type}"
            });

            _logger.LogInformation("MockedLLM returning tool call: {ToolType} with JSON: {Json}", expectedTool.Type, json);

            return Task.FromResult(new LlmResponse
            {
                AssistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = json
                },
                Usage = new LlmUsage
                {
                    PromptTokens = 100,
                    CompletionTokens = 50,
                    TotalTokens = 150
                }
            });
        }

        // After all tools, return stop with "achieved" keyword
        var stopJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            action = "stop",
            reason = "Goal achieved - all tasks completed successfully"
        });

        _logger.LogInformation("MockedLLM returning stop");

        return Task.FromResult(new LlmResponse
        {
            AssistantMessage = new Message
            {
                Role = MessageRole.Assistant,
                Content = stopJson
            },
            Usage = new LlmUsage
            {
                PromptTokens = 100,
                CompletionTokens = 20,
                TotalTokens = 120
            }
        });
    }
}
