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

    [Fact]
    public async Task WriteFile_BasicWrite_WithMockedLlm_Success()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateBasicFileWrite(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.Single(result.ToolInvocations);
        Assert.Equal("write_file", result.ToolInvocations[0].ToolType);

        // Verify file was created
        var newFile = Path.Combine(TestDirectory, "newfile.txt");
        Assert.True(File.Exists(newFile));
        Assert.Equal("Hello, World!", File.ReadAllText(newFile));
    }

    [Fact]
    public async Task WriteFile_BasicWrite_WithRealLlm_Success()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateBasicFileWrite(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was created
        var newFile = Path.Combine(TestDirectory, "newfile.txt");
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public async Task WriteFile_AppendMode_WithMockedLlm_Success()
    {
        // Arrange
        CreateTestFile("append.txt", "Initial content\n");
        var scenario = WriteFileScenarios.CreateAppendToFile(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("mode"));

        // Verify content was appended
        var appendFile = Path.Combine(TestDirectory, "append.txt");
        var content = File.ReadAllText(appendFile);
        Assert.Contains("Initial content", content);
        Assert.Contains("Additional line", content);
    }

    [Fact]
    public async Task WriteFile_AppendMode_WithRealLlm_Success()
    {
        // Arrange
        CreateTestFile("append.txt", "Initial content\n");
        var scenario = WriteFileScenarios.CreateAppendToFile(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify content was appended
        var appendFile = Path.Combine(TestDirectory, "append.txt");
        var content = File.ReadAllText(appendFile);
        Assert.Contains("Initial content", content);
    }

    [Fact]
    public async Task WriteFile_WithBackup_WithMockedLlm_CreatesBackup()
    {
        // Arrange
        CreateTestFile("backup_test.txt", "Original content");
        var scenario = WriteFileScenarios.CreateWriteWithBackup(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("create_backup"));

        // Verify new content was written
        var testFile = Path.Combine(TestDirectory, "backup_test.txt");
        Assert.Equal("New content", File.ReadAllText(testFile));
    }

    [Fact]
    public async Task WriteFile_WithBackup_WithRealLlm_CreatesBackup()
    {
        // Arrange
        CreateTestFile("backup_test.txt", "Original content");
        var scenario = WriteFileScenarios.CreateWriteWithBackup(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify new content was written
        var testFile = Path.Combine(TestDirectory, "backup_test.txt");
        Assert.Equal("New content", File.ReadAllText(testFile));
    }

    [Fact]
    public async Task WriteFile_CreateParentDirectories_WithMockedLlm_CreatesPath()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateCreateParentDirectories(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify nested directories were created
        var nestedFile = Path.Combine(TestDirectory, "nested", "deep", "path", "file.txt");
        Assert.True(File.Exists(nestedFile));
        Assert.Equal("Content", File.ReadAllText(nestedFile));
    }

    [Fact]
    public async Task WriteFile_CreateParentDirectories_WithRealLlm_CreatesPath()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateCreateParentDirectories(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify nested directories were created
        var nestedFile = Path.Combine(TestDirectory, "nested", "deep", "path", "file.txt");
        Assert.True(File.Exists(nestedFile));
    }

    [Fact]
    public async Task WriteFile_WithDifferentEncoding_WithMockedLlm_Success()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateWriteWithDifferentEncoding(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("encoding"));

        // Verify file was written with UTF-16 encoding
        var unicodeFile = Path.Combine(TestDirectory, "unicode_write.txt");
        var content = File.ReadAllText(unicodeFile, System.Text.Encoding.Unicode);
        Assert.Contains("你好世界", content);
    }

    [Fact]
    public async Task WriteFile_WithDifferentEncoding_WithRealLlm_Success()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateWriteWithDifferentEncoding(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify file was written
        var unicodeFile = Path.Combine(TestDirectory, "unicode_write.txt");
        Assert.True(File.Exists(unicodeFile));
    }

    [Fact]
    public async Task WriteFile_OverwriteDisabled_WithMockedLlm_Fails()
    {
        // Arrange
        CreateTestFile("existing_write.txt", "Original content");
        var scenario = WriteFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.False((bool)result.ToolInvocations[0].Parameters["overwrite"]);

        // Verify original content was preserved (write failed)
        var existingFile = Path.Combine(TestDirectory, "existing_write.txt");
        Assert.Equal("Original content", File.ReadAllText(existingFile));
    }

    [Fact]
    public async Task WriteFile_OverwriteDisabled_WithRealLlm_Fails()
    {
        // Arrange
        CreateTestFile("existing_write.txt", "Original content");
        var scenario = WriteFileScenarios.CreateOverwriteDisabled(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);

        // Verify original content was preserved (write failed)
        var existingFile = Path.Combine(TestDirectory, "existing_write.txt");
        Assert.Equal("Original content", File.ReadAllText(existingFile));
    }

    [Fact]
    public async Task WriteFile_PathOutsideAllowed_WithMockedLlm_Fails()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreatePathOutsideAllowed(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

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

    [Fact]
    public async Task WriteFile_PathOutsideAllowed_WithRealLlm_Fails()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreatePathOutsideAllowed(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

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

    [Fact]
    public async Task WriteFile_InvalidEncoding_WithMockedLlm_Fails()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateInvalidEncoding(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
        Assert.True(result.ToolInvocations[0].Parameters.ContainsKey("encoding"));
    }

    [Fact]
    public async Task WriteFile_InvalidEncoding_WithRealLlm_Fails()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateInvalidEncoding(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task WriteFile_InvalidPath_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task WriteFile_InvalidPath_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateInvalidPath(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task WriteFile_MissingRequiredParameters_WithMockedLlm_HandlesError()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateMissingRequiredParameters(TestDirectory);

        // Act
        var result = await RunWithMockedLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }

    [Fact]
    public async Task WriteFile_MissingRequiredParameters_WithRealLlm_HandlesError()
    {
        // Arrange
        var scenario = WriteFileScenarios.CreateMissingRequiredParameters(TestDirectory);

        // Act
        var result = await RunWithRealLlmAsync(scenario);

        // Assert
        AssertBenchmarkSuccess(result, scenario);
    }
}
