namespace Andy.Engine.SweBench.Orchestration;

/// <summary>Classification of a single instance's outcome for fail-fast accounting.</summary>
public enum InstanceOutcome
{
    /// <summary>Ran as expected (resolved, unresolved, empty-patch, model-patch-didn't-apply, max-turns).</summary>
    Soft,

    /// <summary>Indicates broken setup (env/harness error, missing image, exhausted rate limit, malformed row).</summary>
    HardError,
}

/// <summary>
/// Stops a run when the setup is proven broken — never merely because instances fail to
/// resolve. Tracks consecutive hard errors, an early "canary window" error rate, and (in gold
/// mode) trips on the first non-resolving gold instance.
/// </summary>
public sealed class FailFastGate
{
    private readonly int _window;
    private readonly double _threshold;
    private readonly int _maxConsecutive;
    private readonly bool _goldMode;

    private int _seen;
    private int _hardErrorsInWindow;
    private int _consecutiveHardErrors;

    public FailFastGate(int window, double threshold, int maxConsecutiveErrors, bool goldMode)
    {
        _window = Math.Max(1, window);
        _threshold = threshold;
        _maxConsecutive = Math.Max(1, maxConsecutiveErrors);
        _goldMode = goldMode;
    }

    public bool Tripped { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>
    /// Records an outcome and returns true if the run should STOP.
    /// <paramref name="goldResolved"/> is only consulted in gold mode.
    /// </summary>
    public bool Observe(InstanceOutcome outcome, string instanceId, bool? goldResolved = null)
    {
        if (Tripped)
            return true;

        _seen++;

        if (outcome == InstanceOutcome.HardError)
        {
            _consecutiveHardErrors++;
            if (_seen <= _window)
                _hardErrorsInWindow++;
        }
        else
        {
            _consecutiveHardErrors = 0;
        }

        // Gold mode: every gold instance must resolve; the first that doesn't => broken grader.
        if (_goldMode && goldResolved is false)
            return Trip($"gold instance '{instanceId}' did not resolve — grader/Docker setup does not match official");

        if (_consecutiveHardErrors >= _maxConsecutive)
            return Trip($"{_consecutiveHardErrors} consecutive hard errors (last: '{instanceId}')");

        // Canary window: if early instances are mostly hard errors, the setup is broken.
        if (_seen >= _window)
        {
            var rate = (double)_hardErrorsInWindow / _window;
            if (rate >= _threshold && _hardErrorsInWindow > 0 && _seen == _window)
                return Trip($"{_hardErrorsInWindow}/{_window} hard errors in the first {_window} instances (>= {_threshold:P0})");
        }

        return false;
    }

    private bool Trip(string reason)
    {
        Tripped = true;
        Reason = reason;
        return true;
    }
}
