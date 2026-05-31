using System.Text;
using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Grading.LogParsers;
using Andy.Engine.SweBench.Model;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>Pure unit tests for the grading layer (no Docker, no LLM).</summary>
public class GradingUnitTests
{
    [Fact]
    public void DjangoLogParser_Maps_Pass_Fail_Error_Skip()
    {
        const string log = """
            >>>>> Start Test Output
            test_ascii_validator (auth_tests.test_validators.UsernameValidatorsTests) ... ok
            test_unicode_validator (auth_tests.test_validators.UsernameValidatorsTests) ... FAIL
            test_broken (auth_tests.test_validators.UsernameValidatorsTests) ... ERROR
            test_optional (auth_tests.test_validators.UsernameValidatorsTests) ... skipped 'n/a'
            >>>>> End Test Output
            """;

        var map = new DjangoLogParser().Parse(log);

        Assert.Equal(TestStatus.Passed, map["test_ascii_validator (auth_tests.test_validators.UsernameValidatorsTests)"]);
        Assert.Equal(TestStatus.Failed, map["test_unicode_validator (auth_tests.test_validators.UsernameValidatorsTests)"]);
        Assert.Equal(TestStatus.Error, map["test_broken (auth_tests.test_validators.UsernameValidatorsTests)"]);
        Assert.Equal(TestStatus.Skipped, map["test_optional (auth_tests.test_validators.UsernameValidatorsTests)"]);
    }

    [Fact]
    public void StripAnsi_Removes_Colour_Codes_But_Keeps_Parametrized_Test_Ids()
    {
        // Colourised pytest line (astropy 5.1): ESC[32mPASSED ESC[0m id::ESC[1mTest::t ESC[0m
        const string esc = "\u001b";
        var colored = $"{esc}[32mPASSED{esc}[0m astropy/table/tests/test_mixin.py::{esc}[1mtest_attributes[arrayswap]{esc}[0m";

        var clean = DockerGrader.StripAnsi(colored);

        // Exact match proves both that colour codes are removed AND that the "[arrayswap]"
        // parameter bracket survives (the regex only matches ESC-prefixed CSI, not bare brackets).
        Assert.Equal("PASSED astropy/table/tests/test_mixin.py::test_attributes[arrayswap]", clean);
    }

    [Fact]
    public void PytestLogParser_Maps_Pass_Fail_Error_Skip_From_RA_Summary()
    {
        // pytest -rA short-test-summary lines (astropy). FAILED lines carry a " - <reason>" tail.
        const string log = """
            >>>>> Start Test Output
            PASSED astropy/modeling/tests/test_separable.py::test_separable[compound_model6-result6]
            FAILED astropy/units/tests/test_quantity.py::test_round - AssertionError: 1 != 2
            ERROR astropy/io/tests/test_registry.py::test_broken
            SKIPPED astropy/wcs/tests/test_wcs.py::test_optional
            XFAIL astropy/table/tests/test_table.py::test_known_bug
            >>>>> End Test Output
            """;

        var map = new PytestLogParser().Parse(log);

        Assert.Equal(TestStatus.Passed, map["astropy/modeling/tests/test_separable.py::test_separable[compound_model6-result6]"]);
        Assert.Equal(TestStatus.Failed, map["astropy/units/tests/test_quantity.py::test_round"]);
        Assert.Equal(TestStatus.Error, map["astropy/io/tests/test_registry.py::test_broken"]);
        Assert.Equal(TestStatus.Skipped, map["astropy/wcs/tests/test_wcs.py::test_optional"]);
        Assert.Equal(TestStatus.XFail, map["astropy/table/tests/test_table.py::test_known_bug"]);
    }

    [Fact]
    public void PytestLogParser_Maps_Older_Pytest_StatusLast_Verbose_Lines()
    {
        // astropy 1.x pins pytest 3.3.1, which ignores -rA and prints verbose progress lines
        // with the status at the END of the line.
        const string log = """
            >>>>> Start Test Output
            astropy/utils/tests/test_misc.py::test_isiterable PASSED
            astropy/utils/tests/test_misc.py::test_api_lookup SKIPPED
            astropy/utils/tests/test_misc.py::test_broken FAILED
            >>>>> End Test Output
            """;

        var map = new PytestLogParser().Parse(log);

        Assert.Equal(TestStatus.Passed, map["astropy/utils/tests/test_misc.py::test_isiterable"]);
        Assert.Equal(TestStatus.Skipped, map["astropy/utils/tests/test_misc.py::test_api_lookup"]);
        Assert.Equal(TestStatus.Failed, map["astropy/utils/tests/test_misc.py::test_broken"]);
    }

    [Fact]
    public void GradingEngine_Resolved_Only_When_All_F2P_And_P2P_Pass()
    {
        var engine = new GradingEngine();
        var f2p = new[] { "a", "b" };
        var p2p = new[] { "c" };

        var allPass = new Dictionary<string, TestStatus>
        {
            ["a"] = TestStatus.Passed, ["b"] = TestStatus.Passed, ["c"] = TestStatus.Passed,
        };
        Assert.Equal(ResolvedStatus.Full, engine.GetResolutionStatus(f2p, p2p, allPass));

        // Missing test counts as failure -> not resolved.
        var bMissing = new Dictionary<string, TestStatus> { ["a"] = TestStatus.Passed, ["c"] = TestStatus.Passed };
        Assert.Equal(ResolvedStatus.Partial, engine.GetResolutionStatus(f2p, p2p, bMissing));

        // P2P regression -> No, even with all F2P passing.
        var p2pFail = new Dictionary<string, TestStatus>
        {
            ["a"] = TestStatus.Passed, ["b"] = TestStatus.Passed, ["c"] = TestStatus.Failed,
        };
        Assert.Equal(ResolvedStatus.No, engine.GetResolutionStatus(f2p, p2p, p2pFail));

        // No F2P passing -> No.
        var noneF2P = new Dictionary<string, TestStatus> { ["c"] = TestStatus.Passed };
        Assert.Equal(ResolvedStatus.No, engine.GetResolutionStatus(f2p, p2p, noneF2P));
    }

    [Fact]
    public void TestSpecBuilder_Builds_Official_Django_Image_Tag()
    {
        var tag = new TestSpecBuilder().GetImageTag("django__django-12209");
        Assert.Equal("swebench/sweb.eval.x86_64.django_1776_django-12209:latest", tag);
    }

    [Fact]
    public void DiffUtil_Derives_Django_Test_Directives_From_TestPatch()
    {
        const string testPatch =
            "diff --git a/tests/auth_tests/test_validators.py b/tests/auth_tests/test_validators.py\n" +
            "--- a/tests/auth_tests/test_validators.py\n" +
            "+++ b/tests/auth_tests/test_validators.py\n" +
            "@@\n+def test_x(): pass\n";

        var directives = DiffUtil.GetDjangoTestDirectives(testPatch);
        Assert.Equal(new[] { "auth_tests.test_validators" }, directives);
    }

    [Fact]
    public void DiffUtil_GetDiffTargetFiles_Uses_Renamed_Target_Not_Source()
    {
        // astropy-7336: the test patch renames py3_test_quantity_annotations.py to the new name.
        // The directive must be the "b/" target (the file that exists when tests run).
        const string testPatch =
            "diff --git a/astropy/units/tests/py3_test_quantity_annotations.py b/astropy/units/tests/test_quantity_annotations.py\n" +
            "similarity index 95%\n" +
            "rename from astropy/units/tests/py3_test_quantity_annotations.py\n" +
            "rename to astropy/units/tests/test_quantity_annotations.py\n" +
            "--- a/astropy/units/tests/py3_test_quantity_annotations.py\n" +
            "+++ b/astropy/units/tests/test_quantity_annotations.py\n" +
            "@@\n+def test_x(): pass\n";

        var targets = DiffUtil.GetDiffTargetFiles(testPatch);

        Assert.Equal(new[] { "astropy/units/tests/test_quantity_annotations.py" }, targets);
    }

    [Fact]
    public void ModelPatchDecode_Is_Heredoc_Sentinel_Injection_Proof()
    {
        // A model-controlled patch whose CONTENT contains a line exactly equal to the heredoc
        // sentinel would, if embedded raw, terminate the heredoc early and silently truncate the
        // patch. base64-encoding makes the heredoc body pure base64, so the sentinel can never
        // appear in it; the decoded bytes must still be the original patch verbatim.
        var malicious =
            "diff --git a/x.py b/x.py\n" +
            "--- a/x.py\n+++ b/x.py\n@@\n+ok\n" +
            EvalConstants.ModelPatchHeredoc + "\n" +   // the sentinel, embedded inside the patch
            "+more\n";

        var sb = new StringBuilder();
        TestSpecBuilder.AppendModelPatchDecode(sb, malicious);
        var script = sb.ToString();

        // Exactly ONE line equals the sentinel verbatim: the heredoc terminator. The opening line
        // is the full "base64 -d > ... <<'SENTINEL'" command (not a bare sentinel), and the patch
        // content — which itself contains the sentinel string — is now inside the base64 body and
        // therefore never appears as a standalone line. So the heredoc cannot be terminated early.
        var lines = script.Split('\n');
        var sentinelLines = lines.Count(l => l == EvalConstants.ModelPatchHeredoc);
        Assert.Equal(1, sentinelLines);

        // Round-trip: extract the base64 body and confirm it decodes back to the exact patch.
        var open = Array.IndexOf(lines, $"base64 -d > /tmp/model_patch.diff <<'{EvalConstants.ModelPatchHeredoc}'");
        Assert.True(open >= 0, "base64 decode line should be present");
        var body = new StringBuilder();
        for (var i = open + 1; lines[i] != EvalConstants.ModelPatchHeredoc; i++)
            body.Append(lines[i]);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(body.ToString()));
        Assert.Equal(malicious, decoded);
    }

    [Fact]
    public void DiffUtil_FileHeaders_Preserve_Paths_With_Spaces()
    {
        // Git uses a TAB (not a space) to separate an optional timestamp from the diff path, so a
        // path that legitimately contains spaces must survive intact, and a trailing tab+timestamp
        // must be trimmed.
        const string patch =
            "--- a/dir with spaces/mod.py\t2024-01-01 00:00:00\n" +
            "+++ b/dir with spaces/mod.py\t2024-01-01 00:00:00\n" +
            "@@\n+x\n" +
            "--- /dev/null\n" +
            "+++ b/dir with spaces/new file.py\n" +
            "@@\n+y\n";

        Assert.Equal(new[] { "dir with spaces/mod.py" }, DiffUtil.GetModifiedFiles(patch));
        Assert.Equal(new[] { "dir with spaces/new file.py" }, DiffUtil.GetNewFiles(patch));
    }

    [Fact]
    public void Parsers_Return_Empty_Map_On_Empty_Input()
    {
        Assert.Empty(new DjangoLogParser().Parse(string.Empty));
        Assert.Empty(new PytestLogParser().Parse(string.Empty));
    }
}
