using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Andy.Engine.SweBench.Grading;
using Andy.Engine.SweBench.Model;

namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// Provisions a per-instance git workspace and extracts the model patch.
///
/// Repos are bare-cloned once into a cache, then a per-instance working copy is created and
/// checked out at base_commit. The model patch is the working-tree diff after the agent runs,
/// excluding any path the official test patch touches (the agent must not be credited for test
/// edits, and such edits would collide with the grader's test_patch).
/// </summary>
public sealed class SweWorkspaceManager
{
    private readonly string _cacheDir;
    private readonly string _workRoot;
    private readonly TextWriter? _log;

    // One lock per repo so concurrent instances of the same repo don't race to populate the
    // shared bare cache. Created lazily; never removed (a handful of repos per run).
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _repoCloneLocks = new(StringComparer.Ordinal);

    private static readonly string[] ArtifactExcludes =
    {
        "**/__pycache__/**", "**/*.pyc", "**/*.pyo", "**/*.egg-info/**",
        "**/.pytest_cache/**", "**/.tox/**", "**/*.orig", "**/*.rej",
        // Safety copies some models make before editing (e.g. "base.py.backup.20260530212325").
        // These whole-file duplicates are not a fix and must not pollute the model patch.
        "**/*.bak", "**/*.backup", "**/*.backup.*",
    };

    public SweWorkspaceManager(string workDir, TextWriter? log = null)
    {
        _cacheDir = Path.Combine(workDir, "repo-cache");
        _workRoot = Path.Combine(workDir, "workspaces");
        _log = log;
        Directory.CreateDirectory(_cacheDir);
        Directory.CreateDirectory(_workRoot);
    }

    /// <summary>Clones (if needed) and checks out the instance's repo at base_commit. Returns the workspace path.</summary>
    public async Task<string> PrepareAsync(SweBenchInstance instance, CancellationToken cancellationToken = default)
    {
        var bare = await EnsureBareCloneAsync(instance.Repo, cancellationToken);

        var workspace = Path.Combine(_workRoot, Sanitize(instance.InstanceId));
        if (Directory.Exists(workspace))
            Directory.Delete(workspace, recursive: true);

        await GitAsync(_workRoot, cancellationToken, "clone", "--quiet", "--no-checkout", bare, workspace);
        await GitAsync(workspace, cancellationToken, "checkout", "--quiet", instance.BaseCommit);
        // Detach onto a known branch name so the agent's commits/diffs are clean.
        await GitAsync(workspace, cancellationToken, "checkout", "--quiet", "-B", "swebench_run");
        await GitAsync(workspace, cancellationToken, "config", "user.email", "swebench@local");
        await GitAsync(workspace, cancellationToken, "config", "user.name", "swebench");

        return workspace;
    }

    /// <summary>
    /// Returns the working-tree diff (the model patch), excluding files touched by the test patch.
    /// </summary>
    public async Task<string> GetModelPatchAsync(
        string workspace, SweBenchInstance instance, CancellationToken cancellationToken = default)
    {
        // Defensive: some file tools (older Andy.Tools) write UTF-8 with a BOM, prepending
        // EF BB BF to edited files and polluting the diff. The target repos (Python) have no
        // BOM in source, so strip a leading BOM from any changed/new file before diffing.
        await StripIntroducedBomsAsync(workspace, cancellationToken);

        // Stage everything so new files appear in the diff, then diff against the staged tree.
        await GitAsync(workspace, cancellationToken, "add", "-A");

        var excluded = DiffUtil.GetModifiedFiles(instance.TestPatch)
            .Concat(DiffUtil.GetNewFiles(instance.TestPatch))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var args = new List<string> { "diff", "--cached", "--no-color", "--", "." };
        // Exclude test-patch paths via pathspec.
        foreach (var path in excluded)
            args.Add($":(exclude){path}");
        // Exclude common build/cache artifacts so junk never lands in the model patch.
        foreach (var pat in ArtifactExcludes)
            args.Add($":(exclude){pat}");

        var (stdout, _, _) = await GitCaptureAsync(workspace, cancellationToken, args.ToArray());
        return stdout;
    }

    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>Strips a leading UTF-8 BOM from changed/untracked files (Python repos have none).</summary>
    private async Task StripIntroducedBomsAsync(string workspace, CancellationToken cancellationToken)
    {
        var (modified, _, _) = await GitCaptureAsync(workspace, cancellationToken, "diff", "--name-only", "HEAD");
        var (untracked, _, _) = await GitCaptureAsync(workspace, cancellationToken, "ls-files", "--others", "--exclude-standard");

        var files = (modified + "\n" + untracked)
            .Split('\n')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal);

        foreach (var rel in files)
        {
            var full = Path.Combine(workspace, rel);
            if (!File.Exists(full))
                continue;

            var bytes = await File.ReadAllBytesAsync(full, cancellationToken);
            if (bytes.Length < 3 || bytes[0] != Utf8Bom[0] || bytes[1] != Utf8Bom[1] || bytes[2] != Utf8Bom[2])
                continue;

            // Guard against the (rare) case of a genuinely binary file whose first three bytes
            // happen to be EF BB BF: a NUL byte in the prefix means it's not BOM-prefixed text,
            // so leave it untouched rather than corrupting it.
            if (HasNulByte(bytes, 3, 512))
                continue;

            await File.WriteAllBytesAsync(full, bytes[3..], cancellationToken);
        }
    }

    /// <summary>True if any of the first <paramref name="count"/> bytes after <paramref name="start"/> is NUL.</summary>
    private static bool HasNulByte(byte[] bytes, int start, int count)
    {
        var end = Math.Min(bytes.Length, start + count);
        for (var i = start; i < end; i++)
            if (bytes[i] == 0) return true;
        return false;
    }

    /// <summary>Removes a workspace directory (best effort).</summary>
    public void Cleanup(string workspace)
    {
        try { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
        catch { /* best effort */ }
    }

    private async Task<string> EnsureBareCloneAsync(string repo, CancellationToken cancellationToken)
    {
        var bare = Path.Combine(_cacheDir, Sanitize(repo) + ".git");
        if (Directory.Exists(bare))
            return bare;

        // Serialize per repo: with parallel instances, many of the same repo can reach here at
        // once; without this they would clone into the same dir concurrently and corrupt the cache.
        var gate = _repoCloneLocks.GetOrAdd(repo, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(bare))   // another instance populated it while we waited
                return bare;

            // Clone into a temp dir and atomically rename in, so an interrupted/failed clone never
            // leaves a half-populated cache dir that the Directory.Exists check would trust.
            var url = $"https://github.com/{repo}.git";
            _log?.WriteLine($"[git] bare-cloning {url} (first time; cached) ...");
            var tmp = bare + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await GitAsync(_cacheDir, cancellationToken, "clone", "--bare", "--quiet", url, tmp);
                Directory.Move(tmp, bare);
            }
            catch
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
                throw;
            }
            return bare;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task GitAsync(string cwd, CancellationToken ct, params string[] args)
    {
        var (_, stderr, exit) = await GitCaptureAsync(cwd, ct, args);
        if (exit != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed (exit {exit}): {Tail(stderr)}");
    }

    private static async Task<(string Stdout, string Stderr, int Exit)> GitCaptureAsync(
        string cwd, CancellationToken ct, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = new Process { StartInfo = psi };
        var so = new StringBuilder();
        var se = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) so.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) se.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Don't leave an orphaned git process (e.g. a long clone) running after a timeout.
            try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw;
        }
        return (so.ToString(), se.ToString(), p.ExitCode);
    }

    private static string Sanitize(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars().Append('/').ToArray()));

    private static string Tail(string s, int max = 400) => s.Length <= max ? s : s[^max..];
}
