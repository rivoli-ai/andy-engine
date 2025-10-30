using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for read_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class ReadFileTests : FileSystemIntegrationTestBase
{
    public ReadFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task ReadFile_BasicRead_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBasicFileRead(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("read_file", result.ToolInvocations[0].ToolType);
    }

    [Fact]
    public async Task ReadFile_BasicRead_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBasicFileRead(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_JsonFile_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBinaryFileRead(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
    }

    [Fact]
    public async Task ReadFile_JsonFile_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBinaryFileRead(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_WithDifferentEncoding_WithMockedLlm_Success()
    {
        // Arrange
        var unicodeFile = Path.Combine(TestDirectory, "unicode.txt");
        File.WriteAllText(unicodeFile, "Unicode text: 你好世界", System.Text.Encoding.Unicode);
        var scenario = ReadFileScenarios.CreateReadWithDifferentEncoding(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("encoding"));
    }

    [Fact]
    public async Task ReadFile_WithDifferentEncoding_WithRealLlm_Success()
    {
        // Arrange
        var unicodeFile = Path.Combine(TestDirectory, "unicode.txt");
        File.WriteAllText(unicodeFile, "Unicode text: 你好世界", System.Text.Encoding.Unicode);
        var scenario = ReadFileScenarios.CreateReadWithDifferentEncoding(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_SpecificLineRange_WithMockedLlm_Success()
    {
        // Arrange
        var multilineFile = Path.Combine(TestDirectory, "multiline.txt");
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}");
        File.WriteAllLines(multilineFile, lines);
        var scenario = ReadFileScenarios.CreateReadSpecificLineRange(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("start_line"));
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("end_line"));
    }

    [Fact]
    public async Task ReadFile_SpecificLineRange_WithRealLlm_Success()
    {
        // Arrange
        var multilineFile = Path.Combine(TestDirectory, "multiline.txt");
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}");
        File.WriteAllLines(multilineFile, lines);
        var scenario = ReadFileScenarios.CreateReadSpecificLineRange(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_WithMaxSizeLimit_WithMockedLlm_Fails()
    {
        // Arrange
        var largeFile = Path.Combine(TestDirectory, "large.txt");
        var largeContent = new string('A', 200000); // ~200KB
        File.WriteAllText(largeFile, largeContent);
        var scenario = ReadFileScenarios.CreateReadWithMaxSizeLimit(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("max_size_mb"));
    }

    [Fact]
    public async Task ReadFile_WithMaxSizeLimit_WithRealLlm_Fails()
    {
        // Arrange
        var largeFile = Path.Combine(TestDirectory, "large.txt");
        var largeContent = new string('A', 200000); // ~200KB
        File.WriteAllText(largeFile, largeContent);
        var scenario = ReadFileScenarios.CreateReadWithMaxSizeLimit(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_PathOutsideAllowed_WithMockedLlm_Fails()
    {
        // Arrange
        var parentDir = Directory.GetParent(TestDirectory)?.FullName ?? TestDirectory;
        var outsideFile = Path.Combine(parentDir, "outside.txt");
        File.WriteAllText(outsideFile, "test content");
        var scenario = ReadFileScenarios.CreatePathOutsideAllowed(TestDirectory);

        try
        {
            // Act
            var result = await RunWithMockedLlmAsync(scenario);

            // Assert
            AssertBenchmarkSuccess(result, scenario);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outsideFile)) File.Delete(outsideFile);
        }
    }

    [Fact]
    public async Task ReadFile_PathOutsideAllowed_WithRealLlm_Fails()
    {
        // Arrange
        var parentDir = Directory.GetParent(TestDirectory)?.FullName ?? TestDirectory;
        var outsideFile = Path.Combine(parentDir, "outside.txt");
        File.WriteAllText(outsideFile, "test content");
        var scenario = ReadFileScenarios.CreatePathOutsideAllowed(TestDirectory);

        try
        {
            // Act
            var result = await RunWithRealLlmAsync(scenario);

            // Assert
            AssertBenchmarkSuccess(result, scenario);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outsideFile)) File.Delete(outsideFile);
        }
    }

    [Fact]
    public async Task ReadFile_FileNotFound_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateFileNotFound(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_FileNotFound_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateFileNotFound(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_InvalidPath_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_InvalidPath_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_MissingRequiredParameter_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task ReadFile_MissingRequiredParameter_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
