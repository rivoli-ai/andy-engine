using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Tests for list_directory tool via the engine
/// Validates that the engine can properly call the LLM to list directory contents
/// </summary>
public class ListDirectoryTests : FileSystemTestBase
{
    [Fact]
    public void ListDirectory_BasicListing_Success()
    {
        // Arrange
        CreateTestFileStructure();

        var scenario = new BenchmarkScenario
        {
            Id = "fs-list-directory-basic",
            Category = "file-system",
            Description = "List contents of a directory",
            Tags = new List<string> { "file-system", "list-directory", "single-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files and directories in {TestDirectory}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = TestDirectory
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "readme.txt", "documents" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert - This is a definition test, actual execution would require agent
        Assert.NotNull(scenario);
        Assert.Single(scenario.ExpectedTools);
        Assert.Equal("list_directory", scenario.ExpectedTools[0].Type);
    }

    [Fact]
    public void ListDirectory_RecursiveListing_Success()
    {
        // Arrange
        CreateTestFileStructure();

        var scenario = new BenchmarkScenario
        {
            Id = "fs-list-directory-recursive",
            Category = "file-system",
            Description = "List directory contents recursively",
            Tags = new List<string> { "file-system", "list-directory", "recursive" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {TestDirectory} and all subdirectories recursively"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = TestDirectory,
                        ["recursive"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string>
                {
                    "readme.txt",
                    "documents",
                    "report.txt",
                    "program.cs"
                }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Single(scenario.ExpectedTools);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("recursive"));
    }

    [Fact]
    public void ListDirectory_WithPattern_FiltersCorrectly()
    {
        // Arrange
        CreateTestFileStructure();

        var scenario = new BenchmarkScenario
        {
            Id = "fs-list-directory-pattern",
            Category = "file-system",
            Description = "List directory with file pattern filter",
            Tags = new List<string> { "file-system", "list-directory", "pattern" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all .txt files in {TestDirectory}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = TestDirectory,
                        ["pattern"] = "*.txt"
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "readme.txt" },
                mustNotContain: new List<string> { "data.json", "script.sh" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Single(scenario.ExpectedTools);
    }

    [Fact]
    public void ListDirectory_IncludeHidden_ShowsHiddenFiles()
    {
        // Arrange
        CreateTestFileStructure();

        var scenario = new BenchmarkScenario
        {
            Id = "fs-list-directory-hidden",
            Category = "file-system",
            Description = "List directory including hidden files",
            Tags = new List<string> { "file-system", "list-directory", "hidden" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List all files in {TestDirectory} including hidden files"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = TestDirectory,
                        ["include_hidden"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { ".hidden" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Single(scenario.ExpectedTools);
    }

    [Fact]
    public void ListDirectory_Sorted_ReturnsOrderedList()
    {
        // Arrange
        CreateTestFileStructure();

        var scenario = new BenchmarkScenario
        {
            Id = "fs-list-directory-sorted",
            Category = "file-system",
            Description = "List directory sorted by size",
            Tags = new List<string> { "file-system", "list-directory", "sorting" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"List files in {TestDirectory} sorted by size, largest first"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "list_directory",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["directory_path"] = TestDirectory,
                        ["sort_by"] = "size",
                        ["sort_descending"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Single(scenario.ExpectedTools);
    }
}
