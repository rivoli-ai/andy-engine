using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Benchmarks.Validators;

/// <summary>
/// Validates that expected tools were invoked correctly
/// </summary>
public class ToolInvocationValidator : IValidator
{
    public Task<Andy.Engine.Benchmarks.Framework.ValidationResult> ValidateAsync(
        BenchmarkScenario scenario,
        Andy.Engine.Benchmarks.Framework.BenchmarkResult result,
        CancellationToken cancellationToken = default)
    {
        if (scenario.ExpectedTools.Count == 0)
        {
            // No expected tools specified, skip validation
            return Task.FromResult(new Andy.Engine.Benchmarks.Framework.ValidationResult
            {
                ValidatorName = nameof(ToolInvocationValidator),
                Passed = true,
                Message = "No expected tools specified"
            });
        }

        var validationDetails = new Dictionary<string, object>();
        var errors = new List<string>();

        foreach (var expectedTool in scenario.ExpectedTools)
        {
            var matchingInvocations = result.ToolInvocations
                .Where(inv => MatchesTool(inv, expectedTool))
                .ToList();

            var invocationCount = matchingInvocations.Count;

            // Check minimum invocations
            if (invocationCount < expectedTool.MinInvocations)
            {
                errors.Add(
                    $"Tool '{expectedTool.Type}' was invoked {invocationCount} times, " +
                    $"but expected at least {expectedTool.MinInvocations} times");
            }

            // Check maximum invocations
            if (expectedTool.MaxInvocations.HasValue &&
                invocationCount > expectedTool.MaxInvocations.Value)
            {
                errors.Add(
                    $"Tool '{expectedTool.Type}' was invoked {invocationCount} times, " +
                    $"but expected at most {expectedTool.MaxInvocations.Value} times");
            }

            // Validate parameters if specified
            if (expectedTool.Parameters.Count > 0)
            {
                // If there are multiple invocations, prioritize successful ones for parameter validation
                var invocationsToCheck = matchingInvocations.Count > 1
                    ? matchingInvocations.Where(inv => inv.Success).ToList()
                    : matchingInvocations;

                // If all invocations failed, still check them for parameter errors
                if (invocationsToCheck.Count == 0)
                    invocationsToCheck = matchingInvocations;

                foreach (var invocation in invocationsToCheck)
                {
                    var paramErrors = ValidateParameters(expectedTool, invocation);
                    errors.AddRange(paramErrors);
                }
            }

            validationDetails[$"{expectedTool.Type}_invocations"] = invocationCount;
        }

        var passed = errors.Count == 0;
        var message = passed
            ? $"All {scenario.ExpectedTools.Count} expected tool invocation(s) validated successfully"
            : $"Tool invocation validation failed: {string.Join("; ", errors)}";

        return Task.FromResult(new Andy.Engine.Benchmarks.Framework.ValidationResult
        {
            ValidatorName = nameof(ToolInvocationValidator),
            Passed = passed,
            Message = message,
            Details = validationDetails
        });
    }

    private bool MatchesTool(ToolInvocationRecord invocation, ExpectedToolInvocation expected)
    {
        // Check tool type matches
        if (!invocation.ToolType.Equals(expected.Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check path pattern if specified
        if (!string.IsNullOrEmpty(expected.PathPattern))
        {
            // Look for path parameter in the invocation
            if (invocation.Parameters.TryGetValue("path", out var pathObj) ||
                invocation.Parameters.TryGetValue("file_path", out pathObj) ||
                invocation.Parameters.TryGetValue("filePath", out pathObj))
            {
                var path = pathObj?.ToString() ?? "";
                if (!MatchesPattern(path, expected.PathPattern))
                {
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(expected.PathPattern))
            {
                // Path pattern specified but no path parameter found
                return false;
            }
        }

        return true;
    }

    private bool MatchesPattern(string path, string pattern)
    {
        // Simple wildcard matching
        // Convert glob pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")  // ** matches any path
            .Replace("\\*", "[^/]*")  // * matches any file/folder name
            .Replace("\\?", ".")      // ? matches single character
            + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            path,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private List<string> ValidateParameters(
        ExpectedToolInvocation expected,
        ToolInvocationRecord actual)
    {
        var errors = new List<string>();

        foreach (var (paramName, expectedValue) in expected.Parameters)
        {
            if (!actual.Parameters.TryGetValue(paramName, out var actualValue))
            {
                errors.Add(
                    $"Tool '{expected.Type}' missing expected parameter '{paramName}'");
                continue;
            }

            // Simple value comparison (could be enhanced for complex types)
            var expectedStr = expectedValue?.ToString() ?? "";
            var actualStr = actualValue?.ToString() ?? "";

            // Normalize paths for comparison if this is a path parameter
            if (IsPathParameter(paramName))
            {
                expectedStr = NormalizePath(expectedStr);
                actualStr = NormalizePath(actualStr);
            }

            if (!expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Tool '{expected.Type}' parameter '{paramName}' expected '{expectedStr}' " +
                    $"but got '{actualStr}'");
            }
        }

        return errors;
    }

    private bool IsPathParameter(string paramName)
    {
        var pathParams = new[] { "path", "source_path", "destination_path", "file_path",
            "dir_path", "directory_path", "filepath", "dirpath", "source", "destination" };
        return pathParams.Any(p => paramName.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        try
        {
            // Get full path to normalize .. and . sequences
            var normalized = System.IO.Path.GetFullPath(path);

            // Remove trailing directory separators for comparison
            // (e.g., "/path/to/dir/" becomes "/path/to/dir")
            return normalized.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }
        catch
        {
            // If path is invalid, return as-is (but still strip trailing separators)
            return path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }
    }
}
