using System.Text.RegularExpressions;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading.LogParsers;

/// <summary>
/// Faithful port of swebench parse_log_django (log_parsers/python.py). Keys are the
/// full "test_method (module.path.ClassName)" strings (the text before " ... ").
/// </summary>
public sealed partial class DjangoLogParser : ITestLogParser
{
    private static readonly string[] PassSuffixes = { " ... ok", " ... OK", " ...  OK" };

    [GeneratedRegex(@"^(.*?)\s\.\.\.\sTesting against Django installed in ((?s:.*?)) silenced\)\.\nok$", RegexOptions.Multiline)]
    private static partial Regex MultilinePattern1();

    [GeneratedRegex(@"^(.*?)\s\.\.\.\sInternal Server Error: /(.*)/\nok$", RegexOptions.Multiline)]
    private static partial Regex MultilinePattern2();

    [GeneratedRegex(@"^(.*?)\s\.\.\.\sSystem check identified no issues \(0 silenced\)\nok$", RegexOptions.Multiline)]
    private static partial Regex MultilinePattern3();

    public IReadOnlyDictionary<string, TestStatus> Parse(string log)
    {
        var map = new Dictionary<string, TestStatus>(StringComparer.Ordinal);
        string? prevTest = null;

        foreach (var rawLine in log.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Contains("--version is equivalent to version", StringComparison.Ordinal))
                map["--version is equivalent to version"] = TestStatus.Passed;

            if (line.Contains(" ... ", StringComparison.Ordinal))
                prevTest = Split(line, " ... ")[0];

            foreach (var suffix in PassSuffixes)
            {
                if (line.EndsWith(suffix, StringComparison.Ordinal))
                {
                    var work = line;
                    if (work.StartsWith("Applying sites.0002_alter_domain_unique...test_no_migrations", StringComparison.Ordinal))
                        work = Split(work, "...", 2)[^1].Trim();

                    var test = RSplitFirst(work, suffix);
                    map[test] = TestStatus.Passed;
                    break;
                }
            }

            if (line.Contains(" ... skipped", StringComparison.Ordinal))
                map[Split(line, " ... skipped")[0]] = TestStatus.Skipped;

            if (line.EndsWith(" ... FAIL", StringComparison.Ordinal))
                map[Split(line, " ... FAIL")[0]] = TestStatus.Failed;

            if (line.StartsWith("FAIL:", StringComparison.Ordinal) && Tokens(line) is { Length: > 1 } failTok)
                map[failTok[1]] = TestStatus.Failed;

            if (line.EndsWith(" ... ERROR", StringComparison.Ordinal))
                map[Split(line, " ... ERROR")[0]] = TestStatus.Error;

            if (line.StartsWith("ERROR:", StringComparison.Ordinal) && Tokens(line) is { Length: > 1 } errTok)
                map[errTok[1]] = TestStatus.Error;

            if (line.StartsWith("ok", StringComparison.Ordinal) && prevTest is not null)
                map[prevTest] = TestStatus.Passed;
        }

        // Final pass over the raw log for known multiline-interruption cases.
        foreach (var regex in new[] { MultilinePattern1(), MultilinePattern2(), MultilinePattern3() })
        {
            foreach (Match m in regex.Matches(log))
                map[m.Groups[1].Value] = TestStatus.Passed;
        }

        return map;
    }

    // Python str.split(sep) with optional maxsplit; returns the segments.
    private static string[] Split(string s, string sep) => s.Split(sep);

    private static string[] Split(string s, string sep, int maxParts) =>
        s.Split(new[] { sep }, maxParts, StringSplitOptions.None);

    // Python str.rsplit(sep, 1)[0] — everything before the last occurrence of sep.
    private static string RSplitFirst(string s, string sep)
    {
        var idx = s.LastIndexOf(sep, StringComparison.Ordinal);
        return idx < 0 ? s : s[..idx];
    }

    // Python line.split() — whitespace tokens.
    private static string[] Tokens(string s) =>
        s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
