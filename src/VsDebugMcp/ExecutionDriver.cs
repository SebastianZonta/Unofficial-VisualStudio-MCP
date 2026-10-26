using EnvDTE;

namespace VsDebugMcp;

/// <summary>Drives execution control for one debug session: pause, resume with
/// budget, step directions, mode gates, and budget/timeout decisions. Borrows
/// session plumbing (dispatcher, snapshots, COM mapping) from the bridge per
/// call and never sees the session table; logpoint settle and the exception
/// payload stay in the bridge for a later pass.</summary>
internal sealed class ExecutionDriver
{
    private readonly ComDispatcher _dispatcher;
    private readonly Func<string, Func<Debugger, bool>, bool> _issueMap;
    private readonly Func<string, CancellationToken, Task<string>> _snapshotMode;
    private readonly Func<string, CancellationToken, Task<(string? Mode, string? LastHit)>> _trySnapshot;
    private readonly Func<string, CancellationToken, Task> _requireSession;

    public ExecutionDriver(
        ComDispatcher dispatcher,
        Func<string, Func<Debugger, bool>, bool> issueMap,
        Func<string, CancellationToken, Task<string>> snapshotMode,
        Func<string, CancellationToken, Task<(string? Mode, string? LastHit)>> trySnapshot,
        Func<string, CancellationToken, Task> requireSession)
    {
        _dispatcher = dispatcher;
        _issueMap = issueMap;
        _snapshotMode = snapshotMode;
        _trySnapshot = trySnapshot;
        _requireSession = requireSession;
    }

    public async Task<ExecutionResult> PauseAsync(
        string sessionId,
        CancellationToken ct)
    {
        var (currentMode, modeTimeout) = await TryRequireModeAsync(
            sessionId,
            new ExecutionResult("run", null, "timeout", "pause request timed out while reading debugger state; retry"),
            ct);
        if (modeTimeout is not null)
            return modeTimeout;

        DebuggerModes.RequireRunMode(currentMode!);

        try
        {
            // Non-blocking break: Break(true) would park the dispatcher until break
            // mode is reached; poll instead so a timeout never wedges later calls.
            await _dispatcher.InvokeAsync(() =>
            {
                return _issueMap(sessionId, debugger =>
                {
                    debugger.Break(false);
                    return true;
                });
            }, 10_000, ct);
        }
        catch (TimeoutException)
        {
            return new ExecutionResult("run", null, "timeout", "pause request timed out while issuing; retry");
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                return new ExecutionResult("run", null, "timeout", "pause request issued but break mode not reached in time");
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            await _requireSession(sessionId, ct);
            var (mode, lastHit) = await _trySnapshot(sessionId, ct);
            if (mode == "break")
                return new ExecutionResult(mode, lastHit, "ok", "paused on request");
        }
    }

    public async Task<ExecutionResult> ResumeCoreAsync(
        string sessionId,
        int timeoutMs,
        Func<string, string?, int, CancellationToken, Task<bool>> tryLogAndContinue,
        CancellationToken ct)
    {
        var (currentMode, modeTimeout) = await TryRequireModeAsync(
            sessionId,
            new ExecutionResult("unknown", null, "timeout", "resume request timed out while reading debugger state; mode unknown, retry"),
            ct);
        if (modeTimeout is not null)
            return modeTimeout;

        DebuggerModes.RequireNotRunMode(currentMode!);

        if (currentMode == "break")
        {
            // A hit already pending may be a logpoint: log-and-continue it instead
            // of blind-resuming past it (which would silently drop the log entry).
            var (preMode, preHit) = await _trySnapshot(sessionId, ct);
            if (preMode == "break" && await tryLogAndContinue(sessionId, preHit, timeoutMs, ct))
                return await PollResumeAsync(sessionId, DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs), timeoutMs, 1, tryLogAndContinue, ct);
        }

        try
        {
            // Non-blocking Go: returns immediately after resuming so the single STA
            // dispatcher thread is never held hostage by a long-running debuggee.
            // Completion (next breakpoint or process exit) is observed by polling below.
            await _dispatcher.InvokeAsync(() =>
            {
                return _issueMap(sessionId, debugger =>
                {
                    debugger.Go(false);
                    return true;
                });
            }, IssueBudgetMs(timeoutMs), ct);
        }
        catch (TimeoutException)
        {
            // The Go issue may or may not have taken effect: re-snapshot instead of
            // assuming. Running plus a free dispatcher is success, not a timeout.
            var (issueMode, issueHit) = await _trySnapshot(sessionId, ct);
            if (issueMode == "run")
                return new ExecutionResult(issueMode, null, "ok", "resume issued; debuggee running, dispatcher is free");
            var mode = issueMode ?? "unknown";
            return new ExecutionResult(mode, issueMode == "break" ? issueHit : null, "timeout",
                issueMode is null
                    ? "resume request timed out while issuing Go; debugger state unreadable, retry"
                    : $"resume request timed out while issuing Go; debugger mode is {mode}, retry");
        }

        return await PollResumeAsync(sessionId, DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs), timeoutMs, 0, tryLogAndContinue, ct);
    }

    public async Task<ExecutionResult> PollResumeAsync(
        string sessionId,
        DateTime deadline,
        int timeoutMs,
        int logged,
        Func<string, string?, int, CancellationToken, Task<bool>> tryLogAndContinue,
        CancellationToken ct)
    {
        var seenRun = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var remaining = deadline - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                // Go was issued and the dispatcher is free: still-running is success.
                // Re-snapshot so the reported mode is actual, never assumed.
                var (finalMode, finalHit) = await _trySnapshot(sessionId, ct);
                if (finalMode == "break")
                    return new ExecutionResult(finalMode, finalHit, "ok", "hit breakpoint");
                if (finalMode == "design")
                    return new ExecutionResult(finalMode, null, "ok", "run ended back in design mode");
                if (finalMode == "run" || seenRun)
                    return new ExecutionResult("run", null, "ok", "resume issued; debuggee still running, dispatcher is free");
                return new ExecutionResult("unknown", null, "timeout", "resume exceeded timeoutMs; debugger state unreadable, dispatcher is free, retry or pause");
            }

            await Task.Delay(ClampDelay(remaining), ct);
            await _requireSession(sessionId, ct);
            var (mode, lastHit) = await _trySnapshot(sessionId, ct);

            if (mode == "run")
                seenRun = true;

            if (mode == "break")
            {
                // Emulated logpoint: buffer the formatted template and keep going;
                // a logpoint hit never surfaces as a stop.
                if (await tryLogAndContinue(sessionId, lastHit, timeoutMs, ct))
                {
                    logged++;
                    continue;
                }
                // A false here may be a stale snapshot (the Go landed between the
                // snapshot and the re-read) or a transient first-hit automation
                // race, not a genuine stop: confirm with one fresh snapshot and
                // keep polling unless the same break is still pending.
                var (confirmMode, confirmHit) = await _trySnapshot(sessionId, ct);
                if (confirmMode != "break" || !string.Equals(confirmHit, lastHit, StringComparison.OrdinalIgnoreCase))
                    continue;

                return new ExecutionResult(mode, lastHit, "ok",
                    logged > 0 ? $"hit breakpoint; logged {logged} logpoint hits" : "hit breakpoint");
            }

            if (mode == "design")
                return new ExecutionResult(mode, null, "ok", "run ended back in design mode");
        }
    }

    public async Task<ExecutionResult> StepCoreAsync(
        string sessionId,
        string direction,
        int timeoutMs,
        CancellationToken ct)
    {
        var (currentMode, modeTimeout) = await TryRequireModeAsync(
            sessionId,
            new ExecutionResult("unknown", null, "timeout", "step request timed out while reading debugger state; mode unknown, retry or pause"),
            ct);
        if (modeTimeout is not null)
            return modeTimeout;

        DebuggerModes.RequireBreakMode(currentMode!);
        DebuggerModes.RequireStepDirection(direction);

        try
        {
            // Non-blocking step for the same reason as Go(false): never park the
            // dispatcher inside a potentially long debugger wait.
            await _dispatcher.InvokeAsync(() =>
            {
                return _issueMap(sessionId, debugger =>
                {
                    var step = direction switch
                    {
                        "over" => debugger.StepOver,
                        "into" => debugger.StepInto,
                        "out" => (Action<bool>)debugger.StepOut,
                        _ => throw new InvalidOperationException("unreachable: step direction validated before dispatch"),
                    };
                    step(false);
                    return true;
                });
            }, IssueBudgetMs(timeoutMs), ct);
        }
        catch (TimeoutException)
        {
            // The step may or may not have been issued: re-snapshot the actual mode,
            // never report the stale pre-step break.
            var (issueMode, issueHit) = await _trySnapshot(sessionId, ct);
            var mode = issueMode ?? "unknown";
            return new ExecutionResult(mode, issueMode == "break" ? issueHit : null, "timeout",
                issueMode is null
                    ? "step request timed out while issuing; debugger state unreadable, retry or pause"
                    : $"step request timed out while issuing; debugger mode is {mode}, retry or pause");
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var remaining = deadline - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                // Over budget with no terminal state observed: report the actual mode
                // (the step usually resumed the debuggee), never the stale pre-step break.
                var (finalMode, finalHit) = await _trySnapshot(sessionId, ct);
                if (finalMode == "break")
                    return new ExecutionResult(finalMode, finalHit, "ok", $"stepped {direction}");
                if (finalMode == "design")
                    return new ExecutionResult(finalMode, null, "ok", $"stepped {direction}; run ended back in design mode");
                var actual = finalMode ?? "unknown";
                return new ExecutionResult(actual, null, "timeout",
                    finalMode is null
                        ? "step exceeded timeoutMs; debugger state unreadable, retry or pause"
                        : $"step exceeded timeoutMs; debugger mode is {actual}, retry or pause");
            }

            await Task.Delay(ClampDelay(remaining), ct);
            await _requireSession(sessionId, ct);
            var (mode, hit) = await _trySnapshot(sessionId, ct);
            if (mode == "break")
                return new ExecutionResult(mode, hit, "ok", $"stepped {direction}");
            if (mode == "design")
                return new ExecutionResult(mode, null, "ok", $"stepped {direction}; run ended back in design mode");
        }
    }

    /// <summary>Shared Pause/Resume/Step pre-flight: session check plus strict mode read.
    /// A mode-read timeout throws <see cref="ModeReadTimeoutException"/> carrying the
    /// op-specific timeout result, which the caller returns verbatim.</summary>
    private async Task<string> RequireModeAsync(
        string sessionId,
        ExecutionResult timeoutResult,
        CancellationToken ct)
    {
        await _requireSession(sessionId, ct);
        try
        {
            return await _snapshotMode(sessionId, ct);
        }
        catch (TimeoutException)
        {
            throw new ModeReadTimeoutException(timeoutResult);
        }
    }

    private async Task<(string? Mode, ExecutionResult? Timeout)> TryRequireModeAsync(
        string sessionId,
        ExecutionResult timeoutResult,
        CancellationToken ct)
    {
        try
        {
            return (await RequireModeAsync(sessionId, timeoutResult, ct), null);
        }
        catch (ModeReadTimeoutException ex)
        {
            return (null, ex.TimeoutResult);
        }
    }

    private sealed class ModeReadTimeoutException(ExecutionResult timeoutResult) : Exception
    {
        public ExecutionResult TimeoutResult { get; } = timeoutResult;
    }

    private static TimeSpan ClampDelay(TimeSpan remaining) =>
        remaining > TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : remaining;

    /// <summary>Bounds the fire-and-forget issue call so a small timeoutMs is honoured.</summary>
    internal static int IssueBudgetMs(int timeoutMs) => Math.Clamp(timeoutMs, 1_000, 10_000);
}