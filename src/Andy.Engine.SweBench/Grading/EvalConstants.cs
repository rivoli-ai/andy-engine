namespace Andy.Engine.SweBench.Grading;

/// <summary>
/// String markers and commands ported verbatim from the official swebench harness
/// (swebench/harness/constants and run_evaluation.py).
/// </summary>
public static class EvalConstants
{
    public const string ApplyPatchFail = ">>>>> Patch Apply Failed";
    public const string ApplyPatchPass = ">>>>> Applied Patch";
    public const string ResetFailed = ">>>>> Reset Failed";
    public const string TestsError = ">>>>> Tests Errored";
    public const string TestsTimeout = ">>>>> Tests Timed Out";
    public const string StartTestOutput = ">>>>> Start Test Output";
    public const string EndTestOutput = ">>>>> End Test Output";

    /// <summary>Heredoc delimiter used by the official harness for the test patch.</summary>
    public const string TestPatchHeredoc = "EOF_114329324912";

    /// <summary>Delimiter we use for embedding the model patch as a file in the container.</summary>
    public const string ModelPatchHeredoc = "EOF_MODELPATCH_114329324912";

    /// <summary>Fallback chain for applying a patch file (run_evaluation.py GIT_APPLY_CMDS).</summary>
    public static readonly IReadOnlyList<string> GitApplyCmds = new[]
    {
        "git apply --verbose",
        "git apply --verbose --reject",
        "patch --batch --fuzz=5 -p1 -i",
    };

    /// <summary>Extensions treated as non-test files when deriving test directives.</summary>
    public static readonly IReadOnlyList<string> NonTestExts = new[]
    {
        ".json", ".png", "csv", ".txt", ".md", ".jpg", ".jpeg", ".pkl", ".yml", ".yaml", ".toml",
    };

    public const string CondaEnv = "testbed";
    public const string RepoDir = "/testbed";
}
