using System.Diagnostics;
using System.Text;

namespace Andy.Engine.SweBench.Grading;

/// <summary>Drives the `docker` CLI via <see cref="Process"/>. The only place that shells out to docker.</summary>
public sealed class DockerClient : IDockerClient
{
    private readonly string _dockerPath;
    private readonly TextWriter? _log;

    public DockerClient(string dockerPath = "docker", TextWriter? log = null)
    {
        _dockerPath = dockerPath;
        _log = log;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var r = await RunProcessAsync(new[] { "version", "--format", "{{.Server.Version}}" },
            stdin: null, TimeSpan.FromSeconds(20), cancellationToken);
        return r.ExitCode == 0 && !r.TimedOut;
    }

    public async Task<bool> EnsureImageAsync(string image, CancellationToken cancellationToken = default)
    {
        var inspect = await RunProcessAsync(new[] { "image", "inspect", image },
            stdin: null, TimeSpan.FromSeconds(30), cancellationToken);
        if (inspect.ExitCode == 0)
            return true;

        _log?.WriteLine($"[docker] pulling {image} (this can take a while) ...");
        var pull = await RunProcessAsync(new[] { "pull", image },
            stdin: null, TimeSpan.FromMinutes(60), cancellationToken);
        if (pull.ExitCode != 0)
            _log?.WriteLine($"[docker] pull failed for {image}: {Tail(pull.Stderr)}");
        return pull.ExitCode == 0 && !pull.TimedOut;
    }

    public async Task<DockerRunResult> RunAsync(DockerRunSpec spec, CancellationToken cancellationToken = default)
    {
        var args = new[]
        {
            "run", "--rm", "-i",
            "--platform", spec.Platform,
            spec.Image,
            "/bin/bash", "-s",
        };
        return await RunProcessAsync(args, spec.Script, spec.Timeout, cancellationToken);
    }

    private async Task<DockerRunResult> RunProcessAsync(
        IReadOnlyList<string> args, string? stdin, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _dockerPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var outDone = new SemaphoreSlim(0, 1);
        using var errDone = new SemaphoreSlim(0, 1);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) outDone.Release();
            else stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) errDone.Release();
            else stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
        }

        // Drain async readers so we don't lose buffered output.
        await outDone.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await errDone.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        return new DockerRunResult
        {
            ExitCode = timedOut ? -1 : SafeExitCode(process),
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString(),
            TimedOut = timedOut,
        };
    }

    private static int SafeExitCode(Process p)
    {
        try { return p.ExitCode; }
        catch { return -1; }
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }

    private static string Tail(string s, int max = 500) =>
        s.Length <= max ? s : s[^max..];
}
