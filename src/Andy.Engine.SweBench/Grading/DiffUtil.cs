using System.Text.RegularExpressions;

namespace Andy.Engine.SweBench.Grading;

/// <summary>
/// Unified-diff helpers ported from swebench utils.py / test_spec, used to derive
/// reset commands and test directives from a test patch.
/// </summary>
public static partial class DiffUtil
{
    [GeneratedRegex(@"^diff --git a/.* b/(.*)$", RegexOptions.Multiline)]
    private static partial Regex DiffGitTargetRegex();

    /// <summary>Files modified by the patch (source != /dev/null), as repo-relative paths.</summary>
    public static IReadOnlyList<string> GetModifiedFiles(string patch)
    {
        var result = new List<string>();
        foreach (var (source, _) in EnumerateFileHeaders(patch))
        {
            if (source != "/dev/null" && source.StartsWith("a/", StringComparison.Ordinal))
                result.Add(source[2..]);
        }
        return result;
    }

    /// <summary>Files newly added by the patch (source == /dev/null), as repo-relative paths.</summary>
    public static IReadOnlyList<string> GetNewFiles(string patch)
    {
        var result = new List<string>();
        foreach (var (source, target) in EnumerateFileHeaders(patch))
        {
            if (source == "/dev/null" && target.StartsWith("b/", StringComparison.Ordinal))
                result.Add(target[2..]);
        }
        return result;
    }

    /// <summary>
    /// Derives django test directives from a test patch: the "b/" target paths,
    /// excluding non-test extensions, with ".py" and "tests/" stripped and "/" -&gt; ".".
    /// </summary>
    public static IReadOnlyList<string> GetDjangoTestDirectives(string testPatch)
    {
        var directives = new List<string>();
        foreach (Match m in DiffGitTargetRegex().Matches(testPatch))
        {
            var path = m.Groups[1].Value.Trim();
            if (EvalConstants.NonTestExts.Any(ext => path.EndsWith(ext, StringComparison.Ordinal)))
                continue;

            if (path.EndsWith(".py", StringComparison.Ordinal))
                path = path[..^3];
            if (path.StartsWith("tests/", StringComparison.Ordinal))
                path = path["tests/".Length..];
            path = path.Replace('/', '.');
            directives.Add(path);
        }
        return directives;
    }

    /// <summary>
    /// Yields (sourceFile, targetFile) for each file section in a unified diff, reading the
    /// "--- " and "+++ " header lines (whitespace/timestamp suffixes trimmed).
    /// </summary>
    private static IEnumerable<(string Source, string Target)> EnumerateFileHeaders(string patch)
    {
        string? source = null;
        foreach (var rawLine in patch.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                source = FirstToken(line[4..]);
            }
            else if (line.StartsWith("+++ ", StringComparison.Ordinal) && source is not null)
            {
                yield return (source, FirstToken(line[4..]));
                source = null;
            }
        }
    }

    // Diff header paths may carry a trailing tab + timestamp; take the first whitespace-delimited token.
    private static string FirstToken(string s)
    {
        var idx = s.IndexOfAny(new[] { '\t', ' ' });
        return idx >= 0 ? s[..idx] : s;
    }
}
