namespace Andy.Benchmarks.Framework;

public class DiffValidation
{
    /// <summary>
    /// Maximum number of files that should be changed
    /// </summary>
    public int? MaxFilesChanged { get; init; }

    /// <summary>
    /// Expected files to be changed
    /// </summary>
    public List<string> ExpectedFiles { get; init; } = new();

    /// <summary>
    /// Files that should NOT be changed
    /// </summary>
    public List<string> UnexpectedFiles { get; init; } = new();
}