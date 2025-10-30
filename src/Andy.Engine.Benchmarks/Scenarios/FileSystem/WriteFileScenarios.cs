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
            CreateBasicFileWrite(testDirectory),
            CreateAppendToFile(testDirectory)
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
}
