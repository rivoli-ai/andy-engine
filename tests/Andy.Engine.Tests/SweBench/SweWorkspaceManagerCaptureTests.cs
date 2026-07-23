using System.Diagnostics;
using System.Text;
using Andy.Engine.SweBench.Agent;
using FluentAssertions;
using Xunit;

namespace Andy.Engine.Tests.SweBench;

/// <summary>
/// GitCaptureAsync previously read the stdout buffer right after WaitForExitAsync without waiting
/// for the async output drain, so a large `git diff --cached` could come back truncated mid-hunk —
/// and a correct model patch then failed `git apply` (issue #40). This drives a diff well past the
/// OS pipe buffer and asserts the tail arrived.
/// </summary>
public class SweWorkspaceManagerCaptureTests : IDisposable
{
    private readonly string _workspace;

    public SweWorkspaceManagerCaptureTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "andy-swe-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspace);
        Git("init", "-q");
        Git("config", "user.email", "test@local");
        Git("config", "user.name", "test");
        File.WriteAllText(Path.Combine(_workspace, "big.py"), "# placeholder\n");
        Git("add", "-A");
        Git("commit", "-q", "-m", "init");
    }

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); } catch { /* best effort */ }
    }

    private void Git(params string[] args)
    {
        var psi = new ProcessStartInfo { FileName = "git", WorkingDirectory = _workspace };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        p.ExitCode.Should().Be(0, $"git {string.Join(' ', args)} must succeed");
    }

    [Fact]
    public async Task LargeDiff_IsCapturedCompletely()
    {
        // ~3MB of new content — far beyond the 64KB pipe buffer that hid the drain race.
        var content = new StringBuilder();
        for (var i = 0; i < 100_000; i++)
            content.Append("line_").Append(i).Append(" = ").Append(i).Append('\n');
        content.Append("SENTINEL_LAST_LINE = True\n");
        File.WriteAllText(Path.Combine(_workspace, "big.py"), content.ToString());

        var diff = await SweWorkspaceManager.CaptureWorkingTreeDiffAsync(_workspace);

        diff.Should().Contain("SENTINEL_LAST_LINE = True",
            "the tail of a multi-megabyte diff must not be lost to the output-drain race");
        diff.Should().Contain("line_99999 = 99999");
        diff.TrimEnd('\n').Should().NotEndWith("\\", "a diff cut mid-hunk would fail git apply");
    }
}
