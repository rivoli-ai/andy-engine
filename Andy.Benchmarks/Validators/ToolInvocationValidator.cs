using Andy.Benchmarks.Framework;

namespace Andy.Benchmarks.Validators;

/// <summary>
/// Validates that expected tools were invoked correctly
/// </summary>
public class ToolInvocationValidator : IValidator
{
    public Task<ValidationResult> ValidateAsync(
        BenchmarkScenario scenario,
        BenchmarkResult result,
        CancellationToken cancellationToken = default)
    {
        if (scenario.ExpectedTools.Count == 0)
        {
            // No expected tools specified, skip validation
            return Task.FromResult(new ValidationResult
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
                foreach (var invocation in matchingInvocations)
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

        return Task.FromResult(new ValidationResult
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

            if (!expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Tool '{expected.Type}' parameter '{paramName}' expected '{expectedStr}' " +
                    $"but got '{actualStr}'");
            }
        }

        return errors;
    }
}
