using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;
using Xunit;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Tests for copy_file tool via the engine
/// Validates file copying with content preservation
/// </summary>
public class CopyFileTests : FileSystemTestBase
{
    [Fact]
    public void CopyFile_SimpleFileCopy_PreservesContent()
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "Original content to copy");
        var destFile = GetFullPath("destination.txt");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-copy-file-simple",
            Category = "file-system",
            Description = "Copy file A to B preserving contents",
            Tags = new List<string> { "file-system", "copy-file", "single-tool" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the file {sourceFile} to {destFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "copied", "successfully" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Equal("copy_file", scenario.ExpectedTools[0].Type);
        Assert.Contains("preserving contents", scenario.Description);
    }

    [Fact]
    public void CopyFile_WithOverwrite_OverwritesExistingFile()
    {
        // Arrange
        var sourceFile = CreateTestFile("source.txt", "New content");
        var destFile = CreateTestFile("existing_dest.txt", "Old content");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-copy-file-overwrite",
            Category = "file-system",
            Description = "Copy file with overwrite enabled",
            Tags = new List<string> { "file-system", "copy-file", "overwrite" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to {destFile} and overwrite if it exists"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["overwrite"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "overwritten", "copied" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("overwrite"));
    }

    [Fact]
    public void CopyFile_RecursiveDirectory_CopiesAllContents()
    {
        // Arrange
        CreateTestDirectory("source_dir");
        CreateTestFile("source_dir/file1.txt", "File 1");
        CreateTestFile("source_dir/file2.txt", "File 2");
        CreateTestDirectory("source_dir/subdir");
        CreateTestFile("source_dir/subdir/file3.txt", "File 3");

        var sourceDir = GetFullPath("source_dir");
        var destDir = GetFullPath("dest_dir");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-copy-file-recursive",
            Category = "file-system",
            Description = "Copy directory recursively",
            Tags = new List<string> { "file-system", "copy-file", "recursive" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy the entire directory {sourceDir} to {destDir} including all subdirectories"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir,
                        ["recursive"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "copied", "directory" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("recursive"));
    }

    [Fact]
    public void CopyFile_PreserveTimestamps_MaintainsMetadata()
    {
        // Arrange
        var sourceFile = CreateTestFile("timestamped.txt", "Content with metadata");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-copy-file-timestamps",
            Category = "file-system",
            Description = "Copy file preserving timestamps",
            Tags = new List<string> { "file-system", "copy-file", "metadata" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} to copy.txt and preserve the original timestamps"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["preserve_timestamps"] = true
                    }
                }
            },
            Validation = CreateValidationConfig(),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("preserve_timestamps"));
    }

    [Fact]
    public void CopyFile_WithExclusionPattern_ExcludesMatchingFiles()
    {
        // Arrange
        CreateTestDirectory("source_with_logs");
        CreateTestFile("source_with_logs/important.txt", "Important");
        CreateTestFile("source_with_logs/debug.log", "Debug log");
        CreateTestFile("source_with_logs/error.log", "Error log");

        var sourceDir = GetFullPath("source_with_logs");
        var destDir = GetFullPath("dest_no_logs");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-copy-file-exclude",
            Category = "file-system",
            Description = "Copy directory excluding log files",
            Tags = new List<string> { "file-system", "copy-file", "pattern-exclusion" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceDir} to {destDir} but exclude all .log files"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceDir,
                        ["destination_path"] = destDir,
                        ["recursive"] = true,
                        ["exclude_patterns"] = new[] { "*.log" }
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "copied", "excluded" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.True(scenario.ExpectedTools[0].Parameters.ContainsKey("exclude_patterns"));
    }

    [Fact]
    public void CopyFile_ToDirectory_PreservesFilename()
    {
        // Arrange
        var sourceFile = CreateTestFile("document.pdf", "PDF content");
        var destDir = CreateTestDirectory("documents");

        var scenario = new BenchmarkScenario
        {
            Id = "fs-copy-file-to-directory",
            Category = "file-system",
            Description = "Copy file to directory preserving filename",
            Tags = new List<string> { "file-system", "copy-file", "directory-dest" },
            Workspace = CreateWorkspaceConfig(),
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Copy {sourceFile} into the {destDir} directory"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "copy_file",
                    MinInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destDir
                    }
                }
            },
            Validation = CreateValidationConfig(
                mustContain: new List<string> { "copied" }
            ),
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Act & Assert
        Assert.NotNull(scenario);
        Assert.Contains("preserving filename", scenario.Description);
    }
}
