using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Xunit;

namespace Andy.Benchmarks.Scenarios;

/// <summary>
/// Basic tests for single tool invocations
/// </summary>
public class SingleToolTests
{
    [Fact]
    public void ToolInvocationValidator_ValidatesSuccessfulInvocation()
    {
        // Arrange
        var scenario = new BenchmarkScenario
        {
            Id = "test-read-file",
            Category = "single-tool",
            Description = "Test that ReadFile tool is invoked correctly",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Read the file test.txt" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "ReadFile",
                    PathPattern = "**/test.txt",
                    MinInvocations = 1,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig()
        };

        var result = new BenchmarkResult
        {
            ScenarioId = "test-read-file",
            Success = true,
            Duration = TimeSpan.FromSeconds(1),
            StartedAt = DateTime.UtcNow.AddSeconds(-1),
            CompletedAt = DateTime.UtcNow,
            ToolInvocations = new List<ToolInvocationRecord>
            {
                new ToolInvocationRecord
                {
                    ToolType = "ReadFile",
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = "/workspace/test.txt"
                    },
                    Success = true,
                    Timestamp = DateTime.UtcNow,
                    Duration = TimeSpan.FromMilliseconds(100)
                }
            }
        };

        var validator = new ToolInvocationValidator();

        // Act
        var validationResult = validator.ValidateAsync(scenario, result).Result;

        // Assert
        Assert.True(validationResult.Passed, validationResult.Message);
        Assert.Equal(nameof(ToolInvocationValidator), validationResult.ValidatorName);
        Assert.Contains("ReadFile_invocations", validationResult.Details.Keys);
        Assert.Equal(1, validationResult.Details["ReadFile_invocations"]);
    }

    [Fact]
    public void ToolInvocationValidator_FailsWhenToolNotInvoked()
    {
        // Arrange
        var scenario = new BenchmarkScenario
        {
            Id = "test-missing-tool",
            Category = "single-tool",
            Description = "Test that validator fails when expected tool is not invoked",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Write to file output.txt" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "WriteFile",
                    MinInvocations = 1
                }
            },
            Validation = new ValidationConfig()
        };

        var result = new BenchmarkResult
        {
            ScenarioId = "test-missing-tool",
            Success = true,
            Duration = TimeSpan.FromSeconds(1),
            StartedAt = DateTime.UtcNow.AddSeconds(-1),
            CompletedAt = DateTime.UtcNow,
            ToolInvocations = new List<ToolInvocationRecord>() // Empty - no tools invoked
        };

        var validator = new ToolInvocationValidator();

        // Act
        var validationResult = validator.ValidateAsync(scenario, result).Result;

        // Assert
        Assert.False(validationResult.Passed);
        Assert.Contains("WriteFile", validationResult.Message);
        Assert.Contains("0 times", validationResult.Message);
        Assert.Contains("at least 1", validationResult.Message);
    }

    [Fact]
    public void ToolInvocationValidator_ValidatesMultipleInvocations()
    {
        // Arrange
        var scenario = new BenchmarkScenario
        {
            Id = "test-multiple-reads",
            Category = "single-tool",
            Description = "Test that validator handles multiple invocations correctly",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Read all .cs files" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "ReadFile",
                    PathPattern = "**/*.cs",
                    MinInvocations = 2,
                    MaxInvocations = 5
                }
            },
            Validation = new ValidationConfig()
        };

        var result = new BenchmarkResult
        {
            ScenarioId = "test-multiple-reads",
            Success = true,
            Duration = TimeSpan.FromSeconds(2),
            StartedAt = DateTime.UtcNow.AddSeconds(-2),
            CompletedAt = DateTime.UtcNow,
            ToolInvocations = new List<ToolInvocationRecord>
            {
                new ToolInvocationRecord
                {
                    ToolType = "ReadFile",
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = "/workspace/Program.cs"
                    },
                    Success = true,
                    Timestamp = DateTime.UtcNow,
                    Duration = TimeSpan.FromMilliseconds(50)
                },
                new ToolInvocationRecord
                {
                    ToolType = "ReadFile",
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = "/workspace/Helper.cs"
                    },
                    Success = true,
                    Timestamp = DateTime.UtcNow,
                    Duration = TimeSpan.FromMilliseconds(50)
                },
                new ToolInvocationRecord
                {
                    ToolType = "ReadFile",
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = "/workspace/Utils.cs"
                    },
                    Success = true,
                    Timestamp = DateTime.UtcNow,
                    Duration = TimeSpan.FromMilliseconds(50)
                }
            }
        };

        var validator = new ToolInvocationValidator();

        // Act
        var validationResult = validator.ValidateAsync(scenario, result).Result;

        // Assert
        Assert.True(validationResult.Passed, validationResult.Message);
        Assert.Equal(3, validationResult.Details["ReadFile_invocations"]);
    }

    [Fact]
    public void ToolInvocationValidator_FailsWhenTooManyInvocations()
    {
        // Arrange
        var scenario = new BenchmarkScenario
        {
            Id = "test-too-many-invocations",
            Category = "single-tool",
            Description = "Test that validator fails when tool is invoked too many times",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Search files" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "SearchFiles",
                    MinInvocations = 1,
                    MaxInvocations = 2
                }
            },
            Validation = new ValidationConfig()
        };

        var result = new BenchmarkResult
        {
            ScenarioId = "test-too-many-invocations",
            Success = true,
            Duration = TimeSpan.FromSeconds(1),
            StartedAt = DateTime.UtcNow.AddSeconds(-1),
            CompletedAt = DateTime.UtcNow,
            ToolInvocations = new List<ToolInvocationRecord>
            {
                new ToolInvocationRecord { ToolType = "SearchFiles", Success = true, Timestamp = DateTime.UtcNow, Duration = TimeSpan.FromMilliseconds(10) },
                new ToolInvocationRecord { ToolType = "SearchFiles", Success = true, Timestamp = DateTime.UtcNow, Duration = TimeSpan.FromMilliseconds(10) },
                new ToolInvocationRecord { ToolType = "SearchFiles", Success = true, Timestamp = DateTime.UtcNow, Duration = TimeSpan.FromMilliseconds(10) }
            }
        };

        var validator = new ToolInvocationValidator();

        // Act
        var validationResult = validator.ValidateAsync(scenario, result).Result;

        // Assert
        Assert.False(validationResult.Passed);
        Assert.Contains("SearchFiles", validationResult.Message);
        Assert.Contains("3 times", validationResult.Message);
        Assert.Contains("at most 2", validationResult.Message);
    }

    [Fact]
    public void ToolInvocationValidator_SkipsValidationWhenNoExpectedTools()
    {
        // Arrange
        var scenario = new BenchmarkScenario
        {
            Id = "test-no-expectations",
            Category = "single-tool",
            Description = "Test that validator passes when no tools are expected",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Do something" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>(), // No expectations
            Validation = new ValidationConfig()
        };

        var result = new BenchmarkResult
        {
            ScenarioId = "test-no-expectations",
            Success = true,
            Duration = TimeSpan.FromSeconds(1),
            StartedAt = DateTime.UtcNow.AddSeconds(-1),
            CompletedAt = DateTime.UtcNow,
            ToolInvocations = new List<ToolInvocationRecord>
            {
                new ToolInvocationRecord { ToolType = "SomeTool", Success = true, Timestamp = DateTime.UtcNow, Duration = TimeSpan.FromMilliseconds(10) }
            }
        };

        var validator = new ToolInvocationValidator();

        // Act
        var validationResult = validator.ValidateAsync(scenario, result).Result;

        // Assert
        Assert.True(validationResult.Passed);
        Assert.Contains("No expected tools", validationResult.Message);
    }
}
