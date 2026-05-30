namespace Andy.Engine.SweBench.Grading.LogParsers;

/// <summary>
/// Maps a repo (the parser key) to its <see cref="ITestLogParser"/>. Ported subset of
/// swebench MAP_REPO_TO_PARSER. Add entries as repos are added to the subset.
/// </summary>
public sealed class LogParserRegistry
{
    private readonly IReadOnlyDictionary<string, ITestLogParser> _byRepo;

    public LogParserRegistry()
    {
        _byRepo = new Dictionary<string, ITestLogParser>(StringComparer.Ordinal)
        {
            ["django/django"] = new DjangoLogParser(),
        };
    }

    public bool TryGet(string parserKey, out ITestLogParser parser) =>
        _byRepo.TryGetValue(parserKey, out parser!);

    public ITestLogParser Get(string parserKey) =>
        TryGet(parserKey, out var p)
            ? p
            : throw new NotSupportedException($"No log parser registered for '{parserKey}'.");
}
