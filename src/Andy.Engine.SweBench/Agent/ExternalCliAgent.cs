using System.Diagnostics;
using Andy.Engine.SweBench.Orchestration;

namespace Andy.Engine.SweBench.Agent;

/// <summary>
/// Runs an external command-line coding agent (opencode, aider, claude-code, ...) over a
/// workspace. The agent edits files in place; the harness captures the patch afterward via
/// <c>git diff</c>, exactly as for the andy agent — so any CLI agent plugs into the same
/// dataset, grader, and report.
///
/// The command is a whitespace-tokenized template. Whole-token placeholders are substituted
/// (never shell-interpolated, so no injection): <c>{model}</c> → the model id, <c>{workspace}</c>
/// → the workspace path, <c>{prompt}</c> → the problem statement as a single argv element, and
/// <c>{prompt_file}</c> → the path to a temp file holding the problem statement. If the template
/// contains no <c>{prompt}</c>/<c>{prompt_file}</c> token, the problem statement is piped to the
/// process's stdin. The process runs with its working directory set to the workspace.
/// </summary>
public sealed class ExternalCliAgent : ISweAgent
{
    private readonly string _commandTemplate;
    private readonly string _model;
    private readonly string _workspaceDir;
    private readonly TimeSpan _timeout;

    public ExternalCliAgent(string commandTemplate, string model, string workspaceDir, TimeSpan timeout)
    {
        _commandTemplate = commandTemplate;
        _model = model;
        _workspaceDir = workspaceDir;
        _timeout = timeout;
    }

    public async Task<SweAgentRunResult> RunAsync(string problemStatement, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;

        var tokens = _commandTemplate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return new SweAgentRunResult(false, "error: empty --agent-cmd template", 0, TimeSpan.Zero);

        var usesPromptToken = _commandTemplate.Contains("{prompt}", StringComparison.Ordinal)
                           || _commandTemplate.Contains("{prompt_file}", StringComparison.Ordinal);

        string? promptFile = null;
        try
        {
            if (_commandTemplate.Contains("{prompt_file}", StringComparison.Ordinal))
            {
                promptFile = Path.Combine(Path.GetTempPath(), $"swe-prompt-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(promptFile, problemStatement, cancellationToken);
            }

            var psi = new ProcessStartInfo
            {
                FileName = tokens[0],
                WorkingDirectory = _workspaceDir,
                RedirectStandardInput = !usesPromptToken,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var tok in tokens[1..])
            {
                var arg = tok
                    .Replace("{model}", _model, StringComparison.Ordinal)
                    .Replace("{workspace}", _workspaceDir, StringComparison.Ordinal);
                if (tok == "{prompt}")
                    arg = problemStatement;
                else if (tok == "{prompt_file}")
                    arg = promptFile!;
                psi.ArgumentList.Add(arg);
            }

            using var proc = new Process { StartInfo = psi };
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                return new SweAgentRunResult(false, $"error: failed to launch '{tokens[0]}': {ex.Message}",
                    0, DateTime.UtcNow - started);
            }

            if (!usesPromptToken)
            {
                await proc.StandardInput.WriteAsync(problemStatement);
                proc.StandardInput.Close();
            }

            // Drain output concurrently so a chatty agent can't deadlock on a full pipe buffer.
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(proc);
                return new SweAgentRunResult(false, $"timeout after {_timeout.TotalSeconds:0}s",
                    0, DateTime.UtcNow - started);
            }
            catch (OperationCanceledException)
            {
                TryKill(proc);
                throw; // caller-requested cancellation propagates (matches SimpleAgent contract)
            }

            await Task.WhenAll(stdoutTask, stderrTask);
            var duration = DateTime.UtcNow - started;

            if (proc.ExitCode == 0)
                return new SweAgentRunResult(true, "completed", 0, duration);

            var stderr = (await stderrTask).Trim();
            var tail = stderr.Length > 300 ? stderr[^300..] : stderr;
            return new SweAgentRunResult(false, $"exit {proc.ExitCode}: {tail}", 0, duration);
        }
        finally
        {
            if (promptFile is not null)
                try { File.Delete(promptFile); } catch { /* best effort */ }
        }
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
    }

    public void Dispose() { }
}

/// <summary>Builds <see cref="ExternalCliAgent"/> instances from the run configuration.</summary>
public sealed class ExternalCliAgentFactory : ISweAgentFactory
{
    private readonly RunContext _ctx;

    public ExternalCliAgentFactory(RunContext ctx) => _ctx = ctx;

    // The external agent's instructions come from its own config (e.g. opencode AGENTS.md), so the
    // instance/repo is not used here.
    public ISweAgent Create(string workspaceDir, Model.SweBenchInstance instance) =>
        new ExternalCliAgent(
            _ctx.AgentCommand ?? throw new InvalidOperationException(
                "--agent external requires --agent-cmd \"<template>\" (e.g. 'opencode run --model {model} {prompt}')."),
            _ctx.Model,
            workspaceDir,
            TimeSpan.FromSeconds(_ctx.AgentTimeoutSeconds));
}
