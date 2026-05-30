using System.Text;
using Andy.Engine.SweBench.Grading.Constants;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Grading;

/// <summary>The resolved Docker image, test command, parser key and eval script for an instance.</summary>
public sealed record InstanceTestSpec
{
    public required string ImageTag { get; init; }
    public required string TestCommand { get; init; }
    public required string ParserKey { get; init; }

    /// <summary>The bash script run inside the container (model patch apply + eval.sh).</summary>
    public required string EvalScript { get; init; }
}

/// <summary>
/// Builds the per-instance evaluation spec: the official Docker image tag, the repo/version
/// test command with directives appended, and the eval script — all ported from swebench
/// test_spec/python.py and run_evaluation.py.
/// </summary>
public sealed class TestSpecBuilder
{
    private readonly string _namespace;
    private readonly string _arch;
    private readonly string _imageTag;

    public TestSpecBuilder(string @namespace = "swebench", string arch = "x86_64", string imageTag = "latest")
    {
        _namespace = @namespace;
        _arch = arch;
        _imageTag = imageTag;
    }

    /// <summary>
    /// Official instance image key: "sweb.eval.{arch}.{instance_id.lower()}:{tag}", then for a
    /// remote (namespaced) pull, prefixed with "{namespace}/" and "__" -&gt; "_1776_" over the whole string.
    /// </summary>
    public string GetImageTag(string instanceId)
    {
        var key = $"sweb.eval.{_arch}.{instanceId.ToLowerInvariant()}:{_imageTag}";
        return $"{_namespace}/{key}".Replace("__", "_1776_");
    }

    public InstanceTestSpec Build(SweBenchInstance instance, SwePrediction prediction)
    {
        var spec = RepoTestSpecs.TryGet(instance.Repo, instance.Version)
            ?? throw new NotSupportedException(
                $"No test spec for {instance.Repo}@{instance.Version}. Add it to RepoTestSpecs.");

        var directives = GetTestDirectives(instance);
        var testCommand = directives.Count > 0
            ? $"{spec.TestCmd} {string.Join(' ', directives)}"
            : spec.TestCmd;

        var script = BuildEvalScript(instance, spec, prediction.ModelPatch, testCommand);

        return new InstanceTestSpec
        {
            ImageTag = GetImageTag(instance.InstanceId),
            TestCommand = testCommand,
            ParserKey = spec.ParserKey ?? instance.Repo,
            EvalScript = script,
        };
    }

    private static IReadOnlyList<string> GetTestDirectives(SweBenchInstance instance) =>
        instance.Repo == "django/django"
            ? DiffUtil.GetDjangoTestDirectives(instance.TestPatch)
            // Generic default (swebench get_test_directives): the "b/" target paths from the test
            // patch, so a renamed test file (e.g. py3_test_x.py -> test_x.py) resolves to the NEW
            // file that exists when tests run, not the deleted source path.
            : DiffUtil.GetDiffTargetFiles(instance.TestPatch)
                .Where(p => !EvalConstants.NonTestExts.Any(e => p.EndsWith(e, StringComparison.Ordinal)))
                .ToList();

    /// <summary>
    /// Mirrors run_evaluation.py (model-patch apply with fallback chain) + make_eval_script_list_py.
    /// Uses no `set -e` so the script does not exit early (tests must still run / be reverted).
    /// </summary>
    private static string BuildEvalScript(
        SweBenchInstance instance, RepoTestSpec spec, string modelPatch, string testCommand)
    {
        var modified = DiffUtil.GetModifiedFiles(instance.TestPatch);
        var newFiles = DiffUtil.GetNewFiles(instance.TestPatch);

        var sb = new StringBuilder();
        sb.Append("#!/bin/bash\n");
        sb.Append("set -uxo pipefail\n");
        sb.Append($"cd {EvalConstants.RepoDir}\n");

        // ---- Apply the model patch (fallback chain over a written patch file). ----
        sb.Append($"cat > /tmp/model_patch.diff <<'{EvalConstants.ModelPatchHeredoc}'\n");
        sb.Append(modelPatch);
        if (!modelPatch.EndsWith('\n')) sb.Append('\n');
        sb.Append($"{EvalConstants.ModelPatchHeredoc}\n");

        sb.Append("__APPLIED=0\n");
        foreach (var cmd in EvalConstants.GitApplyCmds)
            sb.Append($"if [ \"$__APPLIED\" != \"1\" ]; then {cmd} /tmp/model_patch.diff && __APPLIED=1; fi\n");
        sb.Append($"if [ \"$__APPLIED\" != \"1\" ]; then echo \"{EvalConstants.ApplyPatchFail}\"; exit 0; fi\n");
        sb.Append($"echo \"{EvalConstants.ApplyPatchPass}\"\n");

        // ---- eval.sh body ----
        sb.Append("source /opt/miniconda3/bin/activate\n");
        sb.Append($"conda activate {EvalConstants.CondaEnv}\n");
        sb.Append($"cd {EvalConstants.RepoDir}\n");
        foreach (var c in spec.EvalCommands) sb.Append(c).Append('\n');
        sb.Append($"git config --global --add safe.directory {EvalConstants.RepoDir}\n");
        sb.Append($"cd {EvalConstants.RepoDir}\n");
        sb.Append("git status\n");
        sb.Append("git show\n");
        sb.Append($"git -c core.fileMode=false diff {instance.BaseCommit}\n");
        sb.Append("source /opt/miniconda3/bin/activate\n");
        sb.Append($"conda activate {EvalConstants.CondaEnv}\n");
        if (!string.IsNullOrEmpty(spec.Install)) sb.Append(spec.Install).Append('\n');

        AppendResetCommands(sb, instance.BaseCommit, modified, newFiles);

        // Apply the official test patch via heredoc.
        sb.Append($"git apply -v - <<'{EvalConstants.TestPatchHeredoc}'\n");
        sb.Append(instance.TestPatch);
        if (!instance.TestPatch.EndsWith('\n')) sb.Append('\n');
        sb.Append($"{EvalConstants.TestPatchHeredoc}\n");

        sb.Append($": '{EvalConstants.StartTestOutput}'\n");
        sb.Append(testCommand).Append('\n');
        sb.Append($": '{EvalConstants.EndTestOutput}'\n");

        // Revert tests afterwards.
        AppendResetCommands(sb, instance.BaseCommit, modified, newFiles);

        return sb.ToString();
    }

    private static void AppendResetCommands(
        StringBuilder sb, string baseCommit, IReadOnlyList<string> modified, IReadOnlyList<string> newFiles)
    {
        if (modified.Count > 0)
            sb.Append($"git checkout {baseCommit} {string.Join(' ', modified)}\n");
        if (newFiles.Count > 0)
            sb.Append($"rm -f {string.Join(' ', newFiles)}\n");
    }
}
