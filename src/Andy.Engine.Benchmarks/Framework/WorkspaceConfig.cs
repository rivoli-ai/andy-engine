namespace Andy.Benchmarks.Framework;

/// <summary>
/// Configuration for the workspace where the benchmark runs
/// </summary>
public class WorkspaceConfig
{
    /// <summary>
    /// Type of workspace: "git-clone", "directory-copy", "in-memory"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Source path or URL for the workspace
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Git branch to checkout (if Type is "git-clone")
    /// </summary>
    public string? Branch { get; init; }
}