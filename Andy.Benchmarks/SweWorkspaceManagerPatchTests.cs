using System.Diagnostics;
using Andy.Engine.SweBench.Agent;
using Andy.Engine.SweBench.Model;
using Xunit;

namespace Andy.Benchmarks;

/// <summary>
/// Verifies the model-patch capture excludes junk artifacts. Some models write whole-file
/// safety copies (e.g. "base.py.backup.20260530212325") before editing; those must not pollute
/// the SWE-bench prediction. Uses a real temp git repo (no Docker, no LLM).
/// </summary>
public class SweWorkspaceManagerPatchTests
{
    [Fact]
    public async Task GetModelPatch_Excludes_Backup_Files_But_Keeps_The_Real_Change()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "swebkp_" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(tmp, "ws");
        Directory.CreateDirectory(Path.Combine(workspace, "django", "db", "models"));
        try
        {
            var baseFile = Path.Combine(workspace, "django", "db", "models", "base.py");
            await File.WriteAllTextAsync(baseFile, "line1\nline2\n");
            Git(workspace, "init", "-q");
            Git(workspace, "config", "user.email", "t@t.t");
            Git(workspace, "config", "user.name", "t");
            Git(workspace, "add", "-A");
            Git(workspace, "commit", "-qm", "init");

            // The agent edits the file AND leaves backup copies (observed with mimo-v2.5).
            await File.WriteAllTextAsync(baseFile, "line1\nline2 FIXED\n");
            await File.WriteAllTextAsync(baseFile + ".backup.20260530212325", "line1\nline2 FIXED\n");
            await File.WriteAllTextAsync(baseFile + ".bak", "line1\nline2 FIXED\n");

            var mgr = new SweWorkspaceManager(Path.Combine(tmp, "work"));
            var instance = new SweBenchInstance
            {
                InstanceId = "x__x-1",
                Repo = "x/x",
                BaseCommit = "HEAD",
                ProblemStatement = "p",
            };

            var patch = await mgr.GetModelPatchAsync(workspace, instance);

            // The real change is present...
            Assert.Contains("django/db/models/base.py", patch);
            Assert.Contains("FIXED", patch);
            // ...but the backup copies are not.
            Assert.DoesNotContain(".backup.", patch);
            Assert.DoesNotContain(".bak", patch);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    private static void Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new Xunit.Sdk.XunitException($"git {string.Join(' ', args)} failed: {p.StandardError.ReadToEnd()}");
    }
}
