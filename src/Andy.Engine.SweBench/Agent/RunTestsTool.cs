using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Model;
using Andy.Tools.Core;
using Andy.Tools.Library;

namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// In-loop test tool for the andy SWE-bench agent. Given a test command, it applies the agent's
/// CURRENT edits onto a fresh copy of the repo inside the official Docker image and runs the
/// command, returning the test output so the agent can verify its fix and iterate.
///
/// Constrained, not a general shell: it only runs a test command inside the ephemeral,
/// network-less container (never on the host), and it never injects the instance's hidden tests
/// (see <see cref="SweTestRunner"/>). Bounded by a per-instance invocation cap.
/// </summary>
public sealed class RunTestsTool : ToolBase
{
    private readonly SweBenchInstance _instance;
    private readonly string _workspaceDir;
    private readonly SweTestRunner _runner;
    private readonly int _maxInvocations;
    private int _used;

    public RunTestsTool(SweBenchInstance instance, string workspaceDir, SweTestRunner runner, int maxInvocations)
    {
        _instance = instance;
        _workspaceDir = workspaceDir;
        _runner = runner;
        _maxInvocations = maxInvocations;
    }

    public override ToolMetadata Metadata => new()
    {
        Id = "run_tests",
        Name = "Run Tests",
        Description =
            "Run a test command against your CURRENT edits inside the project's real test environment, " +
            "and return the output. Use it to (1) write and run a reproduction test that fails before your " +
            "fix and passes after, and (2) run the existing tests near the code you changed to check for " +
            "regressions. The command runs from the repository root with the project's test tools available " +
            "(e.g. 'python -m pytest path/to/test_file.py -q' or './tests/runtests.py module.tests'). " +
            "You cannot access the graders' hidden tests. Limited uses per task — spend them deliberately.",
        Category = ToolCategory.Development,
        RequiredCapabilities = ToolCapability.None,
        Parameters = new List<ToolParameter>
        {
            new()
            {
                Name = "test_command",
                Type = "string",
                Required = true,
                Description = "The shell test command to run from the repo root, e.g. " +
                              "\"python -m pytest tests/forms_tests/ -q\".",
            },
        },
    };

    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (_used >= _maxInvocations)
            return ToolResult.Failure(
                $"run_tests limit reached ({_maxInvocations} uses for this task). Make your fix and finish.");

        var command = GetParameter<string>(parameters, "test_command", string.Empty)?.Trim() ?? string.Empty;
        if (command.Length == 0)
            return ToolResult.Failure("test_command is required.");

        _used++;
        var remaining = _maxInvocations - _used;

        string diff;
        try
        {
            diff = await SweWorkspaceManager.CaptureWorkingTreeDiffAsync(_workspaceDir, context.CancellationToken);
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"could not capture your current edits: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(diff))
            return ToolResult.Failure(
                "You have not edited any files yet, so there is nothing to test. Make an edit first " +
                "(or write a reproduction test file) before running tests.");

        var result = await _runner.RunAsync(_instance, diff, command, context.CancellationToken);

        if (result.TimedOut)
            return ToolResult.Failure($"The test command timed out. Run a narrower target. ({remaining} run(s) left.)");
        if (!result.PatchApplied)
            return ToolResult.Failure(
                $"Your current edits did not apply cleanly onto a fresh checkout — check for stray/conflicting " +
                $"changes. ({remaining} run(s) left.)");
        if (result.Error is not null && result.Output.Length == 0)
            return ToolResult.Failure($"{result.Error} ({remaining} run(s) left.)");

        var text = $"[run_tests] `{command}` ({remaining} run(s) left)\n{result.Output}";
        return ToolResult.Success(text);
    }
}
