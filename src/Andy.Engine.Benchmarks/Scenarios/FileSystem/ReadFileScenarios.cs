using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the read_file tool
/// </summary>
public static class ReadFileScenarios
{
    /// <summary>
    /// Creates all read_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            // Happy path scenarios
            CreateBasicFileRead(testDirectory),
            CreateBinaryFileRead(testDirectory),
            CreateReadWithDifferentEncoding(testDirectory),
            CreateReadSpecificLineRange(testDirectory),

            // Safety scenarios (should fail)
            CreateReadWithMaxSizeLimit(testDirectory),
            CreatePathOutsideAllowed(testDirectory),

            // Error handling scenarios
            CreateFileNotFound(testDirectory),
            CreateInvalidPath(testDirectory),
            CreateMissingRequiredParameter(testDirectory)
        };
    }

    /// <summary>
    /// Basic file read operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileRead(string testDirectory)
    {
        var fileToRead = Path.Combine(testDirectory, "readme.txt");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-basic",
            Category = "file-system",
            Description = "Read contents of a text file",
            Tags = new List<string> { "file-system", "read-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read the contents of {fileToRead}"
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
                        ["file_path"] = fileToRead
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "This is the workspace readme" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Read binary file with encoding
    /// </summary>
    public static BenchmarkScenario CreateBinaryFileRead(string testDirectory)
    {
        var fileToRead = Path.Combine(testDirectory, "data.json");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-json",
            Category = "file-system",
            Description = "Read JSON file contents",
            Tags = new List<string> { "file-system", "read-file", "json" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Read the JSON data from {fileToRead}"
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
                        ["file_path"] = fileToRead
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "test" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Read file with different encoding (UTF-16)
    /// </summary>
    public static BenchmarkScenario CreateReadWithDifferentEncoding(string testDirectory)
    {
        var fileToRead = Path.Combine(testDirectory, "unicode.txt");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-encoding",
            Category = "file-system",
            Description = "Read file with UTF-16 encoding",
            Tags = new List<string> { "file-system", "read-file", "encoding" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Read the contents of {fileToRead} using UTF-16 encoding" }
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
                        ["file_path"] = fileToRead,
                        ["encoding"] = "utf-16"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "unicode", "你好" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Read specific line range from file
    /// </summary>
    public static BenchmarkScenario CreateReadSpecificLineRange(string testDirectory)
    {
        var fileToRead = Path.Combine(testDirectory, "multiline.txt");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-line-range",
            Category = "file-system",
            Description = "Read specific line range from file",
            Tags = new List<string> { "file-system", "read-file", "line-range" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Read lines 3 to 5 from {fileToRead}" }
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
                        ["file_path"] = fileToRead,
                        ["start_line"] = 3,
                        ["end_line"] = 5
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "Line 3", "Line 4", "Line 5" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Read file exceeding max size limit (should fail)
    /// </summary>
    public static BenchmarkScenario CreateReadWithMaxSizeLimit(string testDirectory)
    {
        var fileToRead = Path.Combine(testDirectory, "large.txt");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-size-limit",
            Category = "file-system",
            Description = "Read file exceeding max size should fail",
            Tags = new List<string> { "file-system", "read-file", "safety", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Read {fileToRead} with a max size limit of 0.1MB" }
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
                        ["file_path"] = fileToRead,
                        ["max_size_mb"] = 0.1
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "large", "limit", "size" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Read file outside allowed paths (security - should fail)
    /// </summary>
    public static BenchmarkScenario CreatePathOutsideAllowed(string testDirectory)
    {
        var parentDir = Directory.GetParent(testDirectory)?.FullName ?? testDirectory;
        var outsideFile = Path.Combine(parentDir, "outside.txt");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-outside-allowed",
            Category = "file-system",
            Description = "Read file outside allowed paths should fail",
            Tags = new List<string> { "file-system", "read-file", "security", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Read the file {outsideFile}" }
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
                        ["file_path"] = outsideFile
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
    /// File not found error handling
    /// </summary>
    public static BenchmarkScenario CreateFileNotFound(string testDirectory)
    {
        var nonExistentFile = Path.Combine(testDirectory, "nonexistent.txt");

        return new BenchmarkScenario
        {
            Id = "fs-read-file-not-found",
            Category = "file-system",
            Description = "Read non-existent file should fail gracefully",
            Tags = new List<string> { "file-system", "read-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { $"Read the contents of {nonExistentFile}" }
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
                        ["file_path"] = nonExistentFile
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "not found", "does not exist" },
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
            Id = "fs-read-file-invalid-path",
            Category = "file-system",
            Description = "Read with invalid path should fail gracefully",
            Tags = new List<string> { "file-system", "read-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Read a file with an empty path ''" }
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
                        ["file_path"] = ""
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
    /// Missing required parameter error handling
    /// </summary>
    public static BenchmarkScenario CreateMissingRequiredParameter(string testDirectory)
    {
        return new BenchmarkScenario
        {
            Id = "fs-read-file-missing-parameter",
            Category = "file-system",
            Description = "Read without file_path should fail",
            Tags = new List<string> { "file-system", "read-file", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "directory-copy", Source = testDirectory },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Call read_file tool but don't provide the file_path parameter" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "read_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>()
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "required", "parameter", "file_path" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
