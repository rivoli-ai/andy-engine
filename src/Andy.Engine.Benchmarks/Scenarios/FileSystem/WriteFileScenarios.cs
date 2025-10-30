using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the write_file tool
/// </summary>
public static class WriteFileScenarios
{
    /// <summary>
    /// Creates all write_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            // Happy path scenarios
            CreateBasicFileWrite(testDirectory),
            CreateAppendToFile(testDirectory),
            CreateWriteWithBackup(testDirectory),
            CreateCreateParentDirectories(testDirectory),
            CreateWriteWithDifferentEncoding(testDirectory),

            // Safety scenarios (should fail)
            CreateOverwriteDisabled(testDirectory),
            CreatePathOutsideAllowed(testDirectory),
            CreateInvalidEncoding(testDirectory),

            // Error handling scenarios
            CreateInvalidPath(testDirectory),
            CreateMissingRequiredParameters(testDirectory)
        };
    }

    /// <summary>
    /// Basic file write operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileWrite(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "newfile.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-basic",
            Category = "file-system",
            Description = "Write content to a new file",
            Tags = new List<string> { "file-system", "write-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Write 'Hello, World!' to {fileToWrite}"
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "Hello, World!"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "written" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Append content to existing file
    /// </summary>
    public static BenchmarkScenario CreateAppendToFile(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "append.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-append",
            Category = "file-system",
            Description = "Append content to existing file",
            Tags = new List<string> { "file-system", "write-file", "append" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Append 'Additional line' to {fileToWrite}"
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "Additional line",
                        ["mode"] = "append"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "append" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Write file with backup of existing content
    /// </summary>
    public static BenchmarkScenario CreateWriteWithBackup(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "backup_test.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-backup",
            Category = "file-system",
            Description = "Write file creating backup of existing content",
            Tags = new List<string> { "file-system", "write-file", "backup" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Write 'New content' to {fileToWrite}, but create a backup of the existing file first" }
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "New content",
                        ["create_backup"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "written", "backup" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Create parent directories automatically
    /// </summary>
    public static BenchmarkScenario CreateCreateParentDirectories(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "nested", "deep", "path", "file.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-create-dirs",
            Category = "file-system",
            Description = "Write file creating parent directories",
            Tags = new List<string> { "file-system", "write-file", "directories" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Write 'Content' to {fileToWrite}, create directories if needed" }
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "Content",
                        ["overwrite"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "written" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Write file with different encoding (UTF-16)
    /// </summary>
    public static BenchmarkScenario CreateWriteWithDifferentEncoding(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "unicode_write.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-encoding",
            Category = "file-system",
            Description = "Write file with UTF-16 encoding",
            Tags = new List<string> { "file-system", "write-file", "encoding" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Write 'Unicode: 你好世界 🌍' to {fileToWrite} using UTF-16 encoding" }
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "Unicode: 你好世界 🌍",
                        ["encoding"] = "utf-16"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "written", "unicode" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Write file without overwrite (should fail if exists)
    /// </summary>
    public static BenchmarkScenario CreateOverwriteDisabled(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "existing_write.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-no-overwrite",
            Category = "file-system",
            Description = "Write without overwrite should fail when file exists",
            Tags = new List<string> { "file-system", "write-file", "safety", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Write 'New content' to {fileToWrite}, do NOT overwrite if it exists" }
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "New content",
                        ["overwrite"] = false
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "exists", "already", "failed" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Write file outside allowed paths (security - should fail)
    /// </summary>
    public static BenchmarkScenario CreatePathOutsideAllowed(string testDirectory)
    {
        var parentDir = Directory.GetParent(testDirectory)?.FullName ?? testDirectory;
        var outsideFile = Path.Combine(parentDir, "outside_write.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-outside-allowed",
            Category = "file-system",
            Description = "Write file outside allowed paths should fail",
            Tags = new List<string> { "file-system", "write-file", "security", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Write 'Content' to {outsideFile}" }
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
                        ["file_path"] = outsideFile,
                        ["content"] = "Content"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "not within allowed", "permission", "denied" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Write with invalid encoding (should fail)
    /// </summary>
    public static BenchmarkScenario CreateInvalidEncoding(string testDirectory)
    {
        var fileToWrite = Path.Combine(testDirectory, "test.txt");

        return new BenchmarkScenario
        {
            Id = "fs-write-file-invalid-encoding",
            Category = "file-system",
            Description = "Write with invalid encoding should fail",
            Tags = new List<string> { "file-system", "write-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Write 'Content' to {fileToWrite} using 'invalid-encoding' encoding" }
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
                        ["file_path"] = fileToWrite,
                        ["content"] = "Content",
                        ["encoding"] = "invalid-encoding"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "encoding", "invalid", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Invalid path error handling
    /// </summary>
    public static BenchmarkScenario CreateInvalidPath(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-write-file-invalid-path",
            Category = "file-system",
            Description = "Write with invalid path should fail",
            Tags = new List<string> { "file-system", "write-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Write 'Content' to a file with an empty path ''" }
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
                        ["file_path"] = "",
                        ["content"] = "Content"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "invalid", "path", "error" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Missing required parameters error handling
    /// </summary>
    public static BenchmarkScenario CreateMissingRequiredParameters(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-write-file-missing-parameters",
            Category = "file-system",
            Description = "Write without required parameters should fail",
            Tags = new List<string> { "file-system", "write-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Call write_file tool but don't provide the content parameter" }
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
                        ["file_path"] = Path.Combine(testDirectory, "test.txt")
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "required", "parameter", "content" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
