using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for list_directory tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class ListDirectoryTests : FileSystemIntegrationTestBase
{
    public ListDirectoryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_BasicListing_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateBasicListing(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("list_directory", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_RecursiveListing_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateRecursiveListing(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("recursive"));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_WithPattern_FiltersCorrectly(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreatePatternFiltering(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Equal("*.txt", result.ToolInvocations[0].Parameters["pattern"]);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_IncludeHidden_ShowsHiddenFiles(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateHiddenFileInclusion(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True((bool)result.ToolInvocations[0].Parameters["include_hidden"]);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_Sorted_ReturnsOrderedList(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateSortedListing(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Equal("name", result.ToolInvocations[0].Parameters["sort_by"]);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_EmptyDirectory_Success(LlmMode mode)
    {
        // Arrange
        CreateTestDirectory("empty_dir");
        var scenario = ListDirectoryScenarios.CreateEmptyDirectory(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_SortBySize_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateSortBySize(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Equal("size", result.ToolInvocations[0].Parameters["sort_by"]);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_SortDescending_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateSortDescending(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Equal("name", result.ToolInvocations[0].Parameters["sort_by"]);
            Assert.True((bool)result.ToolInvocations[0].Parameters["sort_descending"]);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_MaxDepth_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateMaxDepth(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("max_depth"));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_DirectoryNotFound_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = ListDirectoryScenarios.CreateDirectoryNotFound(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ListDirectory_InvalidPath_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = ListDirectoryScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
