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
}
