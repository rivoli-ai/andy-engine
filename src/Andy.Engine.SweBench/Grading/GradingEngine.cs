using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading;

/// <summary>
/// Ports swebench grading.py: given a parsed status map and an instance's
/// FAIL_TO_PASS / PASS_TO_PASS lists, computes the resolution status.
/// A test absent from the status map counts as a FAILURE.
/// </summary>
public sealed class GradingEngine
{
    /// <summary>test_passed: present AND status is PASSED or XFAIL.</summary>
    public static bool TestPassed(string testCase, IReadOnlyDictionary<string, TestStatus> sm) =>
        sm.TryGetValue(testCase, out var s) && (s == TestStatus.Passed || s == TestStatus.XFail);

    /// <summary>test_failed: absent OR status is FAILED or ERROR.</summary>
    public static bool TestFailed(string testCase, IReadOnlyDictionary<string, TestStatus> sm) =>
        !sm.TryGetValue(testCase, out var s) || s == TestStatus.Failed || s == TestStatus.Error;

    /// <summary>
    /// Computes the resolution status using the PASS_AND_FAIL evaluation type
    /// (the default; django uses it). Returns FULL only when every FAIL_TO_PASS
    /// passes AND every PASS_TO_PASS passes.
    /// </summary>
    public ResolvedStatus GetResolutionStatus(
        IReadOnlyList<string> failToPass,
        IReadOnlyList<string> passToPass,
        IReadOnlyDictionary<string, TestStatus> statusMap)
    {
        var f2p = Fraction(failToPass, statusMap);
        var p2p = Fraction(passToPass, statusMap);

        if (f2p == 1d && p2p == 1d) return ResolvedStatus.Full;
        if (f2p is < 1d and > 0d && p2p == 1d) return ResolvedStatus.Partial;
        return ResolvedStatus.No;
    }

    /// <summary>
    /// Success fraction for a category under PASS_AND_FAIL: each case is classified
    /// as success (test_passed) or failure (test_failed); an empty category yields 1.
    /// </summary>
    private static double Fraction(IReadOnlyList<string> cases, IReadOnlyDictionary<string, TestStatus> sm)
    {
        int success = 0, failure = 0;
        foreach (var c in cases)
        {
            if (TestPassed(c, sm)) success++;
            else if (TestFailed(c, sm)) failure++;
        }
        var total = success + failure;
        return total == 0 ? 1d : (double)success / total;
    }
}
