namespace Andy.Engine.SweBench.Grading;

/// <summary>Spec for a single `docker run`: image, the bash script (fed via stdin), and a timeout.</summary>
public sealed record DockerRunSpec
{
    public required string Image { get; init; }

    /// <summary>Bash script piped to `bash -s` on the container's stdin.</summary>
    public required string Script { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>e.g. "linux/x86_64".</summary>
    public string Platform { get; init; } = "linux/x86_64";
}

public sealed record DockerRunResult
{
    public int ExitCode { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string Stderr { get; init; } = string.Empty;
    public bool TimedOut { get; init; }

    /// <summary>Combined stdout+stderr, as the harness parses interleaved test output.</summary>
    public string Combined => string.IsNullOrEmpty(Stderr) ? Stdout : $"{Stdout}\n{Stderr}";
}

/// <summary>Abstraction over the `docker` CLI, so the grader can be unit-tested without Docker.</summary>
public interface IDockerClient
{
    /// <summary>True if the docker daemon is reachable.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>True if the image exists locally or can be pulled.</summary>
    Task<bool> EnsureImageAsync(string image, CancellationToken cancellationToken = default);

    /// <summary>Runs the script in a fresh container and returns the captured output.</summary>
    Task<DockerRunResult> RunAsync(DockerRunSpec spec, CancellationToken cancellationToken = default);
}
