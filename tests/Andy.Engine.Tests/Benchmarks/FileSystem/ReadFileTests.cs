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

    [Theory]
    [LlmTestData]
    public async Task ReadFile_BasicRead_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBasicFileRead(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("read_file", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_JsonFile_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFileStructure();
        var scenario = ReadFileScenarios.CreateBinaryFileRead(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_WithDifferentEncoding_Success(LlmMode mode)
    {
        // Arrange
        var unicodeFile = Path.Combine(TestDirectory, "unicode.txt");
        File.WriteAllText(unicodeFile, "Unicode text: 你好世界", System.Text.Encoding.Unicode);
        var scenario = ReadFileScenarios.CreateReadWithDifferentEncoding(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("encoding"));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_SpecificLineRange_Success(LlmMode mode)
    {
        // Arrange
        var multilineFile = Path.Combine(TestDirectory, "multiline.txt");
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}");
        File.WriteAllLines(multilineFile, lines);
        var scenario = ReadFileScenarios.CreateReadSpecificLineRange(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("start_line"));
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("end_line"));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_WithMaxSizeLimit_Fails(LlmMode mode)
    {
        // Arrange
        var largeFile = Path.Combine(TestDirectory, "large.txt");
        var largeContent = new string('A', 200000); // ~200KB
        File.WriteAllText(largeFile, largeContent);
        var scenario = ReadFileScenarios.CreateReadWithMaxSizeLimit(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("max_size_mb"));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_PathOutsideAllowed_Fails(LlmMode mode)
    {
        // Arrange
        var parentDir = Directory.GetParent(TestDirectory)?.FullName ?? TestDirectory;
        var outsideFile = Path.Combine(parentDir, "outside.txt");
        File.WriteAllText(outsideFile, "test content");
        var scenario = ReadFileScenarios.CreatePathOutsideAllowed(TestDirectory);

        try
        {
            // Act
            var result = await RunAsync(scenario, mode);

            // Assert
            AssertBenchmarkSuccess(result, scenario);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outsideFile)) File.Delete(outsideFile);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_FileNotFound_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateFileNotFound(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_InvalidPath_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task ReadFile_MissingRequiredParameter_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = ReadFileScenarios.CreateMissingRequiredParameter(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
