using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the delete_file tool
/// </summary>
public static class DeleteFileScenarios
{
    /// <summary>
    /// Creates all delete_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            CreateBasicFileDeletion(testDirectory),
            CreateRecursiveDirectoryDeletion(testDirectory)
        };
    }

    /// <summary>
    /// Basic file deletion operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileDeletion(string testDirectory)
    {
        var fileToDelete = Path.Combine(testDirectory, "deleteme.txt");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-basic",
            Category = "file-system",
            Description = "Delete a single file",
            Tags = new List<string> { "file-system", "delete-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the file {fileToDelete}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = fileToDelete
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Recursive directory deletion
    /// </summary>
    public static BenchmarkScenario CreateRecursiveDirectoryDeletion(string testDirectory)
    {
        var dirToDelete = Path.Combine(testDirectory, "delete_dir");

        return new BenchmarkScenario
        {
            Id = "fs-delete-file-recursive",
            Category = "file-system",
            Description = "Delete directory recursively",
            Tags = new List<string> { "file-system", "delete-file", "recursive" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Delete the entire directory {dirToDelete} and all its contents"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "delete_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_path"] = dirToDelete,
                        ["recursive"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "deleted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
