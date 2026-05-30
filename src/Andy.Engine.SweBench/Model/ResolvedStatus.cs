namespace Andy.Engine.SweBench.Model;

/// <summary>
/// Resolution status for an instance, matching swebench grading.py semantics.
/// Only <see cref="Full"/> counts as "resolved".
/// </summary>
public enum ResolvedStatus
{
    /// <summary>All FAIL_TO_PASS pass AND all PASS_TO_PASS pass.</summary>
    Full,

    /// <summary>Some but not all FAIL_TO_PASS pass, with PASS_TO_PASS fully passing.</summary>
    Partial,

    /// <summary>Otherwise.</summary>
    No,
}
