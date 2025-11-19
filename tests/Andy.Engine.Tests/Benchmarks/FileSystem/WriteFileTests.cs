using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Scenarios.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Integration tests for write_file tool via the engine
/// Executes scenarios through the Agent with both mocked and real LLM
/// </summary>
public class WriteFileTests : FileSystemIntegrationTestBase
{
    public WriteFileTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_BasicWrite_Success(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateBasicFileWrite(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("write_file", result.ToolInvocations[0].ToolType);
        }

        // Verify file was created
        var newFile = Path.Combine(TestDirectory, "newfile.txt");
        Assert.True(File.Exists(newFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("Hello, World!", File.ReadAllText(newFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_AppendMode_Success(LlmMode mode)
    {
        // Arrange
        CreateTestFile("append.txt", "Initial content\n");
        var scenario = WriteFileScenarios.CreateAppendToFile(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("append"));
        }

        // Verify content was appended
        var appendFile = Path.Combine(TestDirectory, "append.txt");
        var content = File.ReadAllText(appendFile);
        Assert.Contains("Initial content", content);

        if (mode == LlmMode.Mock)
        {
            Assert.Contains("Additional line", content);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_WithBackup_CreatesBackup(LlmMode mode)
    {
        // Arrange
        CreateTestFile("backup_test.txt", "Original content");
        var scenario = WriteFileScenarios.CreateWriteWithBackup(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_backup"));
        }

        // Verify new content was written
        var testFile = Path.Combine(TestDirectory, "backup_test.txt");
        Assert.Equal("New content", File.ReadAllText(testFile));
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_CreateParentDirectories_CreatesPath(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateCreateParentDirectories(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify nested directories were created
        var nestedFile = Path.Combine(TestDirectory, "nested", "deep", "path", "file.txt");
        Assert.True(File.Exists(nestedFile));

        if (mode == LlmMode.Mock)
        {
            Assert.Equal("Content", File.ReadAllText(nestedFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_WithDifferentEncoding_Success(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateWriteWithDifferentEncoding(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("encoding"));

            // Verify file was written with UTF-16 encoding
            var unicodeFile = Path.Combine(TestDirectory, "unicode_write.txt");
            var content = File.ReadAllText(unicodeFile, System.Text.Encoding.Unicode);
            Assert.Contains("你好世界", content);
        }
        else
        {
            // Verify file was written
            var unicodeFile = Path.Combine(TestDirectory, "unicode_write.txt");
            Assert.True(File.Exists(unicodeFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_OverwriteDisabled_Fails(LlmMode mode)
    {
        // Arrange
        CreateTestFile("existing_write.txt", "Original content");
        var scenario = WriteFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.False((bool)result.ToolInvocations[0].Parameters["overwrite"]);
        }

        // Verify original content was preserved (write failed)
        var existingFile = Path.Combine(TestDirectory, "existing_write.txt");
        Assert.Equal("Original content", File.ReadAllText(existingFile));
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_PathOutsideAllowed_Fails(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreatePathOutsideAllowed(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was NOT created outside test directory
        var parentDir = Directory.GetParent(TestDirectory)?.FullName;
        if (parentDir != null)
        {
            var outsideFile = Path.Combine(parentDir, "outside_write.txt");
            Assert.False(File.Exists(outsideFile));
        }
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_InvalidEncoding_Fails(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateInvalidEncoding(TestDirectory);

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
    public async Task WriteFile_InvalidPath_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Theory]
    [LlmTestData]
    public async Task WriteFile_MissingRequiredParameters_HandlesError(LlmMode mode)
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateMissingRequiredParameters(TestDirectory);

        // Act
        var result = await RunAsync(scenario, mode);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
