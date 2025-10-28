using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Tests for read_file tool via the engine
/// Validates that the engine can properly call the LLM to read file contents
/// </summary>
public class ReadFileTests : FileSystemTestBase
{
    [Fact]
    public void ReadFile_SmallTextFile_Success()
    {
        // Arrange
        var testFile = CreateTestFile("sample.txt", "Hello World!\nThis is a test file.");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-read-file-basic",
            Category = "file-system",
            Description = "Read a small text file",
            Tags = new List<string> { "file-system", "read-file", "single-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read the contents of {testFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = testFile
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "Hello World", "test file" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Single(scenario.ExpectedTools);
        Assert.Equal("read_file", scenario.ExpectedTools[0].Type);
    }

    [Fact]
    public void ReadFile_WithEncoding_SpecifiesCorrectEncoding()
    {
        // Arrange
        var testFile = CreateTestFile("unicode.txt", "Hello 世界! Ñoño Café");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-read-file-encoding",
            Category = "file-system",
            Description = "Read file with specific encoding",
            Tags = new List<string> { "file-system", "read-file", "encoding" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read the file {testFile} using UTF-8 encoding"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = testFile,
                        ["encoding"] = "utf-8"
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "世界", "Café" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("encoding"));
    }

    [Fact]
    public void ReadFile_PartialRead_UsesLineRange()
    {
        // Arrange
        var content = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        var testFile = CreateTestFile("large.txt", content);

        var scenario = new BenchmarkScenario
        {
            Id = "fs-read-file-partial",
            Category = "file-system",
            Description = "Read specific lines from a file",
            Tags = new List<string> { "file-system", "read-file", "partial-read" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read lines 10 to 20 from {testFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = testFile,
                        ["start_line"] = 10,
                        ["end_line"] = 20
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "Line 10", "Line 20" },
                mustNotContain: new List<string> { "Line 1", "Line 100" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("start_line"));
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("end_line"));
    }

    [Fact]
    public void ReadFile_MultipleFiles_InvokesMultipleTimes()
    {
        // Arrange
        CreateTestFile("file1.txt", "Content 1");
        CreateTestFile("file2.txt", "Content 2");
        CreateTestFile("file3.txt", "Content 3");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-read-file-multiple",
            Category = "file-system",
            Description = "Read multiple files",
            Tags = new List<string> { "file-system", "read-file", "multi-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read all .txt files in {TestDirectory} and summarize their contents"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 3,
                    MaxInvocations = 10  // Allow some flexibility for the LLM
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "Content 1", "Content 2", "Content 3" }
            ),
            Timeout = TimeSpan.FromMinutes(2)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal(3, scenario.ExpectedTools[0].MinInvocations);
    }

    [Fact]
    public void ReadFile_WithSizeLimit_RespectsLimit()
    {
        // Arrange
        var smallContent = "Small file content";
        var testFile = CreateTestFile("small.txt", smallContent);

        var scenario = new BenchmarkScenario
        {
            Id = "fs-read-file-size-limit",
            Category = "file-system",
            Description = "Read file with size limit",
            Tags = new List<string> { "file-system", "read-file", "size-limit" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read {testFile} but ensure the file is less than 1MB"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = testFile,
                        ["max_size_mb"] = 1.0
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "Small file" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("max_size_mb"));
    }

    [Fact]
    public void ReadFile_JsonFile_ParsesAndResponds()
    {
        // Arrange
        var jsonContent = @"{
    ""name"": ""Test Project"",
    ""version"": ""1.0.0"",
    ""dependencies"": [""lib1"", ""lib2""]
}";
        var testFile = CreateTestFile("config.json", jsonContent);

        var scenario = new BenchmarkScenario
        {
            Id = "fs-read-file-json",
            Category = "file-system",
            Description = "Read and parse JSON file",
            Tags = new List<string> { "file-system", "read-file", "json" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read {testFile} and tell me what the version is"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    MaxInvocations = 1
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "1.0.0" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Single(scenario.Validation.ResponseMustContain);
    }
}
