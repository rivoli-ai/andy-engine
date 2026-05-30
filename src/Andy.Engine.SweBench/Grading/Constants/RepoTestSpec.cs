namespace Andy.Engine.SweBench.Grading.Constants;

/// <summary>
/// Per-repo+version evaluation spec (subset of swebench MAP_REPO_VERSION_TO_SPECS).
/// Only the fields needed at grade time against a prebuilt instance image.
/// </summary>
public sealed record RepoTestSpec
{
    /// <summary>The base test command (test ids/directives are appended).</summary>
    public required string TestCmd { get; init; }

    /// <summary>Install command re-run inside eval.sh (e.g. "python -m pip install -e ."). May be null.</summary>
    public string? Install { get; init; }

    /// <summary>Pre-test environment commands (e.g. locale exports). May be empty.</summary>
    public IReadOnlyList<string> EvalCommands { get; init; } = Array.Empty<string>();

    /// <summary>Log-parser key (defaults to the repo name).</summary>
    public string? ParserKey { get; init; }
}
