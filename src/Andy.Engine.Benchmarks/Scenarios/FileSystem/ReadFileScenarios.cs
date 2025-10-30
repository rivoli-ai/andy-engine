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
            CreateBasicFileRead(testDirectory),
            CreateBinaryFileRead(testDirectory)
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
}
