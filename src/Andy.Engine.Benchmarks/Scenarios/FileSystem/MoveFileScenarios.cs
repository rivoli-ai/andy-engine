using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.FileSystem;

/// <summary>
/// Provides benchmark scenarios for the move_file tool
/// </summary>
public static class MoveFileScenarios
{
    /// <summary>
    /// Creates all move_file benchmark scenarios
    /// </summary>
    /// <param name="testDirectory">The test directory path to use in scenarios</param>
    public static List<BenchmarkScenario> CreateScenarios(string testDirectory)
    {
        return new List<BenchmarkScenario>
        {
            CreateBasicFileMove(testDirectory),
            CreateMoveWithOverwrite(testDirectory)
        };
    }

    /// <summary>
    /// Basic file move operation
    /// </summary>
    public static BenchmarkScenario CreateBasicFileMove(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "moveme.txt");
        var destFile = Path.Combine(testDirectory, "moved.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-basic",
            Category = "file-system",
            Description = "Move file from source to destination",
            Tags = new List<string> { "file-system", "move-file", "single-tool" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move the file {sourceFile} to {destFile}"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "moved" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Move file with overwrite enabled
    /// </summary>
    public static BenchmarkScenario CreateMoveWithOverwrite(string testDirectory)
    {
        var sourceFile = Path.Combine(testDirectory, "moveme.txt");
        var destFile = Path.Combine(testDirectory, "existing.txt");

        return new BenchmarkScenario
        {
            Id = "fs-move-file-overwrite",
            Category = "file-system",
            Description = "Move file with overwrite enabled",
            Tags = new List<string> { "file-system", "move-file", "overwrite" },
            Workspace = new WorkspaceConfig
            {
                Type = "directory-copy",
                Source = testDirectory
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    $"Move {sourceFile} to {destFile} and overwrite if it exists"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "move_file",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["source_path"] = sourceFile,
                        ["destination_path"] = destFile,
                        ["overwrite"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "moved" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
