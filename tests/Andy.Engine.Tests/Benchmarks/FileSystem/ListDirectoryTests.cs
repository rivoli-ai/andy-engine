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

    [Fact]
    public async Task ListDirectory_BasicListing_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateBasicListing(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("list_directory", result.ToolInvocations[0].ToolType);
    }

    [Fact]
    public async Task ListDirectory_BasicListing_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateBasicListing(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ListDirectory_RecursiveListing_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateRecursiveListing(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("recursive"));
    }

    [Fact]
    public async Task ListDirectory_RecursiveListing_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateRecursiveListing(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ListDirectory_WithPattern_WithMockedLlm_FiltersCorrectly()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreatePatternFiltering(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Equal("*.txt", result.ToolInvocations[0].Parameters["pattern"]);
    }

    [Fact]
    public async Task ListDirectory_WithPattern_WithRealLlm_FiltersCorrectly()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreatePatternFiltering(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ListDirectory_IncludeHidden_WithMockedLlm_ShowsHiddenFiles()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateHiddenFileInclusion(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True((bool)result.ToolInvocations[0].Parameters["include_hidden"]);
    }

    [Fact]
    public async Task ListDirectory_IncludeHidden_WithRealLlm_ShowsHiddenFiles()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateHiddenFileInclusion(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ListDirectory_Sorted_WithMockedLlm_ReturnsOrderedList()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateSortedListing(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Equal("name", result.ToolInvocations[0].Parameters["sort_by"]);
    }

    [Fact]
    public async Task ListDirectory_Sorted_WithRealLlm_ReturnsOrderedList()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ListDirectoryScenarios.CreateSortedListing(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
