using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading.LogParsers;

/// <summary>
/// Faithful port of swebench parse_log_pytest_v2 (log_parsers/python.py), the parser mapped to
/// astropy/astropy. Handles two pytest output shapes:
///   - status-first  "PASSED path::test"     (the "-rA" short-test-summary, modern pytest)
///   - status-last   "path::test PASSED"      (the verbose progress line, older pytest that
///                                              ignores "-rA", e.g. astropy 1.x's pytest 3.3.1)
/// FAILED summary lines carry a " - &lt;reason&gt;" tail that is collapsed first.
/// </summary>
public sealed class PytestLogParser : ITestLogParser
{
    private static readonly (string Token, TestStatus Status)[] Statuses =
    {
        ("PASSED", TestStatus.Passed),
        ("FAILED", TestStatus.Failed),
        ("ERROR", TestStatus.Error),
        ("SKIPPED", TestStatus.Skipped),
        ("XFAIL", TestStatus.XFail),
    };

    public IReadOnlyDictionary<string, TestStatus> Parse(string log)
    {
        var map = new Dictionary<string, TestStatus>(StringComparer.Ordinal);

        foreach (var rawLine in log.Split('\n'))
        {
            // Status-first: "PASSED <id>" (pytest -rA summary).
            if (StartsWithStatus(rawLine, out var leading))
            {
                var line = leading == TestStatus.Failed ? rawLine.Replace(" - ", " ") : rawLine;
                var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 2)
                    map[tokens[1]] = leading;
            }
            // Status-last: "<id> PASSED" (older pytest verbose progress lines).
            else if (EndsWithStatus(rawLine, out var trailing))
            {
                var tokens = rawLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 2)
                    map[tokens[0]] = trailing;
            }
        }

        return map;
    }

    private static bool StartsWithStatus(string line, out TestStatus status)
    {
        foreach (var (token, s) in Statuses)
        {
            if (line.StartsWith(token, StringComparison.Ordinal))
            {
                status = s;
                return true;
            }
        }
        status = default;
        return false;
    }

    private static bool EndsWithStatus(string line, out TestStatus status)
    {
        foreach (var (token, s) in Statuses)
        {
            if (line.EndsWith(token, StringComparison.Ordinal))
            {
                status = s;
                return true;
            }
        }
        status = default;
        return false;
    }
}
