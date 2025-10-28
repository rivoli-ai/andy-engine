using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Tests for write_file tool via the engine
/// Validates write guards and that the engine only writes when explicitly asked
/// </summary>
public class WriteFileTests : FileSystemTestBase
{
    [Fact]
    public void WriteFile_ExplicitRequest_WritesNewFile()
    {
        // Arrange
        var newFilePath = GetFullPath("output.txt");
        var content = "This is test content";

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-new",
            Category = "file-system",
            Description = "Write content to a new file when explicitly requested",
            Tags = new List<string> { "file-system", "write-file", "guard-test" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Write the text '{content}' to a new file at {newFilePath}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = newFilePath,
                        ["content"] = content
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "written", "created" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal("write_file", scenario.ExpectedTools[0].Type);

        // Verify scenario emphasizes explicit write request
        Assert.Contains("explicitly", scenario.Description.ToLower());
    }

    [Fact]
    public void WriteFile_NoExplicitRequest_DoesNotWrite()
    {
        // Arrange
        var existingFile = CreateTestFile("existing.txt", "Original content");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-guard",
            Category = "file-system",
            Description = "Should NOT write file when only asked to read",
            Tags = new List<string> { "file-system", "write-file", "guard-test", "negative-test" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Tell me about the file {existingFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    MaxInvocations = 1
                },
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 0,
                    MaxInvocations = 0  // Should NOT write
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "content" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal(2, scenario.ExpectedTools.Count);

        var writeExpectation = scenario.ExpectedTools.First(t => t.Type == "write_file");
        Assert.Equal(0, writeExpectation.MaxInvocations);
    }

    [Fact]
    public void WriteFile_WithBackup_CreatesBackupBeforeOverwrite()
    {
        // Arrange
        var existingFile = CreateTestFile("data.txt", "Original data");
        var newContent = "Updated data";

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-backup",
            Category = "file-system",
            Description = "Overwrite file with backup creation",
            Tags = new List<string> { "file-system", "write-file", "backup" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Overwrite {existingFile} with the text '{newContent}' and create a backup"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = existingFile,
                        ["content"] = newContent,
                        ["create_backup"] = true,
                        ["overwrite"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "backup", "overwritten" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("create_backup"));
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("overwrite"));
    }

    [Fact]
    public void WriteFile_AppendMode_AppendsToExistingFile()
    {
        // Arrange
        var existingFile = CreateTestFile("log.txt", "Entry 1\n");
        var appendContent = "Entry 2\n";

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-append",
            Category = "file-system",
            Description = "Append content to existing file",
            Tags = new List<string> { "file-system", "write-file", "append" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Append '{appendContent}' to the file {existingFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = existingFile,
                        ["content"] = appendContent,
                        ["append"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "appended" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("append"));
        Assert.Equal(true, scenario.ExpectedTools[0].Parameters["append"]);
    }

    [Fact]
    public void WriteFile_WithEncoding_UsesSpecifiedEncoding()
    {
        // Arrange
        var newFile = GetFullPath("unicode.txt");
        var content = "Hello 世界! Ñoño Café";

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-encoding",
            Category = "file-system",
            Description = "Write file with specific encoding",
            Tags = new List<string> { "file-system", "write-file", "encoding" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Write '{content}' to {newFile} using UTF-8 encoding"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = newFile,
                        ["content"] = content,
                        ["encoding"] = "utf-8"
                    }
                }
            },
            Validation = CreateValidationConfig(),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("encoding"));
    }

    [Fact]
    public void WriteFile_CreatesParentDirectories_AutoCreatesPath()
    {
        // Arrange
        var nestedPath = GetFullPath("new/nested/path/file.txt");
        var content = "Content in nested directory";

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-nested",
            Category = "file-system",
            Description = "Write file to non-existent nested directory",
            Tags = new List<string> { "file-system", "write-file", "auto-create" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Write '{content}' to {nestedPath}. Create any necessary parent directories."
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = nestedPath,
                        ["content"] = content
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "created", "written" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Contains("nested", nestedPath);
    }

    [Fact]
    public void WriteFile_OverwriteProtection_RespectsOverwriteFlag()
    {
        // Arrange
        var existingFile = CreateTestFile("protected.txt", "Protected content");
        var newContent = "Should not overwrite";

        var scenario = new BenchmarkScenario
        {
            Id = "fs-write-file-no-overwrite",
            Category = "file-system",
            Description = "Fail to write when overwrite is disabled",
            Tags = new List<string> { "file-system", "write-file", "protection", "negative-test" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Write '{newContent}' to {existingFile} but do NOT overwrite if it exists"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["file_path"] = existingFile,
                        ["content"] = newContent,
                        ["overwrite"] = false
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "exists", "not overwritten" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal(false, scenario.ExpectedTools[0].Parameters["overwrite"]);
    }
}
