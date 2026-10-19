using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using Microsoft.Extensions.Logging;

namespace VsDebugMcp;

/// <summary>Live bridge: EnvDTE automation against the ROT-discovered VS 2026 instance.
/// Every COM call runs on the single <see cref="ComDispatcher"/> STA thread; live handles
/// are snapshotted to plain DTOs inside the same call and never escape.</summary>
internal sealed partial class DteDebugBridge : IDebugBridge, IDisposable
{
    private readonly ComDispatcher _dispatcher = new();
    private readonly ILogger<DteDebugBridge>? _logger;
    private DTE? _dte;
    private string? _activeSessionId;
    private bool _disposed;

    public DteDebugBridge(ILogger<DteDebugBridge>? logger = null)
    {
        _logger = logger;
    }

    public async Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken ct)
    {
        var dte = await _dispatcher.InvokeAsync(() => BindRunningInstance(instanceOverride), 10_000, ct);
        if (_dte is not null)
        {
            try { Marshal.ReleaseComObject(_dte); } catch { /* best effort */ }
        }
        _dte = dte;
        _activeSessionId = Guid.NewGuid().ToString("N");
        var (mode, lastHit) = await ReadStateAsync(ct);
        return new AttachResult(_activeSessionId, mode, lastHit);
    }

    public Task RequireSessionAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_activeSessionId is null || sessionId != _activeSessionId)
        {
            if (_activeSessionId is null)
                throw new DebugDomainException("no-session", "no active session", "call attach first");
            throw new DebugDomainException(
                "stale-session", $"session {sessionId} was replaced by a newer attach", "use the latest sessionId");
        }
        if (_dte is null)
            throw new DebugDomainException("vs-not-running", "the bound VS instance is gone", "call attach again");
        return Task.CompletedTask;
    }

    public async Task<ExecutionResult> AttachProcessAsync(
        string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        // Consent first: no VS touched without it.
        if (!consent)
            throw new DebugDomainException(
                "consent-required", $"attaching to process {processId} needs explicit consent",
                "re-call attach_process with consent: true");
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(_dte!);
                EnvDTE.Process? target = null;
                foreach (EnvDTE.Process candidate in debugger.LocalProcesses)
                {
                    if (candidate.ProcessID == processId) { target = candidate; break; }
                }
                if (target is null)
                    throw new DebugDomainException(
                        "process-not-found", $"no local process {processId}", "list local processes and retry");
                var owner = ProcessOwner.OwnerOf(processId);
                var me = ProcessOwner.CurrentUserName();
                if (owner is null)
                    throw new DebugDomainException(
                        "user-mismatch", $"could not determine the owner of process {processId}; failing closed",
                        "only attach to processes you own");
                if (!owner.Equals(me, StringComparison.OrdinalIgnoreCase))
                    throw new DebugDomainException(
                        "user-mismatch", $"process {processId} belongs to {owner}, bridge runs as {me}",
                        "only attach to your own processes");
                target.Attach();
                return new ExecutionResult(ModeOf(debugger), LastHitOf(debugger),
                    "ok", $"attached to process {processId} ({target.Name})");
            }, timeoutMs, ct);
        }
        catch (TimeoutException)
        {
            throw new DebugDomainException(
                "timeout", $"attaching to process {processId} exceeded timeoutMs", "retry with a larger timeoutMs");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }
    }

    public async Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            debugger.Breakpoints.Add(
                Function: "", File: file, Line: line, Column: 1,
                Condition: "", ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue,
                Language: "",
                Data: "", DataCount: 0, Address: "", HitCount: 0,
                HitCountType: dbgHitCountType.dbgHitCountTypeNone);
            // Add returns the collection; the just-added breakpoint is the last item.
            var added = debugger.Breakpoints.Item(debugger.Breakpoints.Count);
            var info = SnapshotBreakpoint(added, debugger.Breakpoints.Count);
            // Automation cannot observe pending-vs-bound (Concord-only); a successful Add means VS accepted it.
            return new SetBreakpointResult(info with { Bound = true }, ModeOf(debugger), LastHitOf(debugger));
        }, ct);

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            var items = new List<BreakpointInfo>();
            var index = 0;
            foreach (Breakpoint bp in debugger.Breakpoints)
            {
                index++;
                items.Add(SnapshotBreakpoint(bp, index));
            }
            return new ListBreakpointsResult(items, ModeOf(debugger), LastHitOf(debugger));
        }, ct);

    public async Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            var target = FindByToken(debugger, breakpointId);
            if (target is null)
                throw new DebugDomainException(
                    "breakpoint-not-found", $"no breakpoint {breakpointId}", "call list_breakpoints for valid ids");
            var id = TokenOf(target, debugger);
            target.Delete();
            return new RemoveBreakpointResult(id, ModeOf(debugger), LastHitOf(debugger));
        }, ct);

    public async Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct)
    {
        string currentMode;
        try
        {
            currentMode = await RequireModeAsync(sessionId,
                new ExecutionResult("run", null, "timeout", "pause request timed out while reading debugger state; retry"), ct);
        }
        catch (ModeReadTimeoutException ex)
        {
            return ex.TimeoutResult;
        }
        if (currentMode != "run")
            throw new DebugDomainException(
                "not-running", $"debuggee is {currentMode}, nothing to pause", "resume first, then pause while running");
        try
        {
            // Non-blocking break: Break(true) would park the dispatcher until break
            // mode is reached; poll instead so a timeout never wedges later calls.
            await _dispatcher.InvokeAsync(() => { DebuggerOf(_dte!).Break(false); return true; }, 10_000, ct);
        }
        catch (TimeoutException)
        {
            return new ExecutionResult("run", null, "timeout", "pause request timed out while issuing; retry");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                return new ExecutionResult("run", null, "timeout", "pause request issued but break mode not reached in time");
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            await RequireSessionAsync(sessionId, ct);
            var (mode, lastHit) = await TrySnapshotAsync(ct);
            if (mode == "break")
                return new ExecutionResult(mode, lastHit, "ok", "paused on request");
        }
    }

    public async Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct)
    {
        string currentMode;
        try
        {
            currentMode = await RequireModeAsync(sessionId,
                new ExecutionResult("unknown", null, "timeout", "resume request timed out while reading debugger state; mode unknown, retry"), ct);
        }
        catch (ModeReadTimeoutException ex)
        {
            return ex.TimeoutResult;
        }
        if (currentMode == "run")
            throw new DebugDomainException(
                "already-running", "debuggee is already running", "pause first to inspect");
        try
        {
            // Non-blocking Go: returns immediately after resuming so the single STA
            // dispatcher thread is never held hostage by a long-running debuggee.
            // Completion (next breakpoint or process exit) is observed by polling below.
            await _dispatcher.InvokeAsync(() => { DebuggerOf(_dte!).Go(false); return true; }, IssueBudgetMs(timeoutMs), ct);
        }
        catch (TimeoutException)
        {
            // The Go issue may or may not have taken effect: re-snapshot instead of
            // assuming. Running plus a free dispatcher is success, not a timeout.
            var (issueMode, issueHit) = await TrySnapshotAsync(ct);
            if (issueMode == "run")
                return new ExecutionResult(issueMode, null, "ok", "resume issued; debuggee running, dispatcher is free");
            var mode = issueMode ?? "unknown";
            return new ExecutionResult(mode, issueMode == "break" ? issueHit : null, "timeout",
                issueMode is null
                    ? "resume request timed out while issuing Go; debugger state unreadable, retry"
                    : $"resume request timed out while issuing Go; debugger mode is {mode}, retry");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        var seenRun = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                // Go was issued and the dispatcher is free: still-running is success.
                // Re-snapshot so the reported mode is actual, never assumed.
                var (finalMode, finalHit) = await TrySnapshotAsync(ct);
                if (finalMode == "break")
                    return new ExecutionResult(finalMode, finalHit, "ok", "hit breakpoint");
                if (finalMode == "design")
                    return new ExecutionResult(finalMode, null, "ok", "run ended back in design mode");
                if (finalMode == "run" || seenRun)
                    return new ExecutionResult("run", null, "ok", "resume issued; debuggee still running, dispatcher is free");
                return new ExecutionResult("unknown", null, "timeout", "resume exceeded timeoutMs; debugger state unreadable, dispatcher is free, retry or pause");
            }
            await Task.Delay(ClampDelay(remaining), ct);
            await RequireSessionAsync(sessionId, ct);
            var (mode, lastHit) = await TrySnapshotAsync(ct);
            if (mode == "run")
                seenRun = true;
            if (mode == "break")
                return new ExecutionResult(mode, lastHit, "ok", "hit breakpoint");
            if (mode == "design")
                return new ExecutionResult(mode, null, "ok", "run ended back in design mode");
        }
    }

    public async Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct)
    {
        string currentMode;
        try
        {
            currentMode = await RequireModeAsync(sessionId,
                new ExecutionResult("unknown", null, "timeout", "step request timed out while reading debugger state; mode unknown, retry or pause"), ct);
        }
        catch (ModeReadTimeoutException ex)
        {
            return ex.TimeoutResult;
        }
        if (currentMode != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {currentMode}",
                "resume to a breakpoint or pause first; only inspect and step in break mode");
        if (direction is not ("over" or "into" or "out"))
            throw new DebugDomainException("invalid-argument", $"unknown step direction {direction}", "pass over, into, or out");
        try
        {
            // Non-blocking step for the same reason as Go(false): never park the
            // dispatcher inside a potentially long debugger wait.
            await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(_dte!);
                switch (direction)
                {
                    case "over": debugger.StepOver(false); break;
                    case "into": debugger.StepInto(false); break;
                    case "out": debugger.StepOut(false); break;
                }
                return true;
            }, IssueBudgetMs(timeoutMs), ct);
        }
        catch (TimeoutException)
        {
            // The step may or may not have been issued: re-snapshot the actual mode,
            // never report the stale pre-step break.
            var (issueMode, issueHit) = await TrySnapshotAsync(ct);
            var mode = issueMode ?? "unknown";
            return new ExecutionResult(mode, issueMode == "break" ? issueHit : null, "timeout",
                issueMode is null
                    ? "step request timed out while issuing; debugger state unreadable, retry or pause"
                    : $"step request timed out while issuing; debugger mode is {mode}, retry or pause");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
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
                var (finalMode, finalHit) = await TrySnapshotAsync(ct);
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
            await RequireSessionAsync(sessionId, ct);
            var (mode, _) = await TrySnapshotAsync(ct);
            if (mode == "break")
                return new ExecutionResult(mode, null, "ok", $"stepped {direction}");
            if (mode == "design")
                return new ExecutionResult(mode, null, "ok", $"stepped {direction}; run ended back in design mode");
        }
    }

    public async Task<ReadStackResult> ReadStackAsync(string sessionId, int maxFrames, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            RequireBreak(debugger);
            var frames = new List<StackFrameInfo>();
            var index = 0;
            foreach (StackFrame frame in debugger.CurrentThread.StackFrames)
            {
                if (frames.Count >= maxFrames) break;
                // EnvDTE exposes no file/line on stack frames (Concord-only); function+module identify the frame.
                frames.Add(new StackFrameInfo($"f{index++}", frame.FunctionName, frame.Module, null, null));
            }
            return new ReadStackResult(frames, ModeOf(debugger), LastHitOf(debugger));
        }, ct);

    public async Task<ReadVariablesResult> ReadVariablesAsync(
        string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            RequireBreak(debugger);
            if (path is not null)
            {
                // Read-only evaluate: no auto-evaluate, so inspecting never runs user code as a side effect.
                var evaluated = debugger.GetExpression(path, false, 5000);
                if (!evaluated.IsValidValue)
                    throw new DebugDomainException(
                        "path-not-found", $"no expandable path {path}", "drill down only via expandable paths returned by read_variables");
                return new ReadVariablesResult(frameId, path, SnapshotMembers(evaluated, path, maxVariables),
                    ModeOf(debugger), LastHitOf(debugger));
            }
            var frame = FindFrame(debugger, frameId);
            if (frame is null)
                throw new DebugDomainException(
                    "frame-not-found", $"no frame {frameId}", "call read_stack for valid frame ids");
            debugger.CurrentStackFrame = frame;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var vars = new List<ValueInfo>();
            foreach (Expression local in frame.Locals)
            {
                if (vars.Count >= maxVariables) break;
                if (seen.Add(local.Name)) vars.Add(SnapshotValue(local, null));
            }
            foreach (Expression arg in frame.Arguments)
            {
                if (vars.Count >= maxVariables) break;
                if (seen.Add(arg.Name)) vars.Add(SnapshotValue(arg, null));
            }
            return new ReadVariablesResult(frameId, null, vars, ModeOf(debugger), LastHitOf(debugger));
        }, ct);

    public async Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            RequireBreak(debugger);
            // Read-only evaluate: no auto-evaluate, so inspecting never runs user code as a side effect.
            var evaluated = debugger.GetExpression(expression, false, timeoutMs);
            if (!evaluated.IsValidValue)
                throw new DebugDomainException(
                    "invalid-expression", $"expression parsed but did not evaluate: {expression}",
                    "pass a read-only expression valid in the current frame");
            return SnapshotEvaluation(evaluated, expression, ModeOf(debugger), LastHitOf(debugger));
        }, ct);

    // ---- helpers (all run on the dispatcher thread) ----

    private async Task<T> RunSnapshotAsync<T>(string sessionId, Func<Debugger, T> work, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        try
        {
            return await _dispatcher.InvokeAsync(() => work(DebuggerOf(_dte!)), 30_000, ct);
        }
        catch (TimeoutException)
        {
            throw new DebugDomainException(
                "timeout", "VS automation call exceeded its budget", "dismiss any modal dialog in VS and retry");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }
    }

    private static Debugger DebuggerOf(DTE dte) => dte.Debugger;

    private static void RequireBreak(Debugger debugger)
    {
        if (ModeOf(debugger) != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {ModeOf(debugger)}",
                "resume to a breakpoint or pause first; only inspect and step in break mode");
    }

    private static string ModeOf(Debugger debugger) => debugger.CurrentMode switch
    {
        dbgDebugMode.dbgBreakMode => "break",
        dbgDebugMode.dbgRunMode => "run",
        _ => "design",
    };

    private static string? LastHitOf(Debugger debugger)
    {
        try
        {
            var hit = debugger.BreakpointLastHit;
            return hit is null ? null : $"{hit.File}:{hit.FileLine}";
        }
        catch (COMException)
        {
            return null;
        }
    }

    private async Task<(string Mode, string? LastHit)> ReadStateAsync(CancellationToken ct)
    {
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(_dte!);
                return (ModeOf(debugger), LastHitOf(debugger));
            }, 10_000, ct);
        }
        catch (Exception ex) when (ex is TimeoutException or COMException)
        {
            return ("design", null);
        }
    }

    /// <summary>Fast strict snapshot for pre-flight mode checks; throws on timeout/COM.</summary>
    private async Task<string> SnapshotModeAsync(CancellationToken ct)
    {
        return await _dispatcher.InvokeAsync(() => ModeOf(DebuggerOf(_dte!)), 10_000, ct);
    }

    /// <summary>Shared Pause/Resume/Step pre-flight: session check plus strict mode read.
    /// A mode-read timeout throws <see cref="ModeReadTimeoutException"/> carrying the
    /// op-specific timeout result, which the caller returns verbatim.</summary>
    private async Task<string> RequireModeAsync(string sessionId, ExecutionResult timeoutResult, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        try
        {
            return await SnapshotModeAsync(ct);
        }
        catch (TimeoutException)
        {
            throw new ModeReadTimeoutException(timeoutResult);
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }
    }

    private sealed class ModeReadTimeoutException(ExecutionResult timeoutResult) : Exception
    {
        public ExecutionResult TimeoutResult { get; } = timeoutResult;
    }

    /// <summary>Best-effort snapshot for polling: null mode means "keep waiting".</summary>
    private async Task<(string? Mode, string? LastHit)> TrySnapshotAsync(CancellationToken ct)
    {
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(_dte!);
                return ((string?)ModeOf(debugger), LastHitOf(debugger));
            }, 5_000, ct);
        }
        catch (Exception ex) when (ex is TimeoutException or COMException or OperationCanceledException)
        {
            TraceFallback(ex, "transient debugger snapshot failed; polling continues");
            return (null, null);
        }
    }

    private static TimeSpan ClampDelay(TimeSpan remaining)
    {
        if (remaining > TimeSpan.FromMilliseconds(250))
            return TimeSpan.FromMilliseconds(250);
        return remaining;
    }

    /// <summary>Bounds the fire-and-forget issue call so a small timeoutMs is honoured.</summary>
    private static int IssueBudgetMs(int timeoutMs) => Math.Clamp(timeoutMs, 1_000, 10_000);

    private static int IndexOf(Debugger debugger, Breakpoint target)
    {
        var index = 0;
        foreach (Breakpoint bp in debugger.Breakpoints)
        {
            index++;
            if (ReferenceEquals(bp, target)) return index;
        }
        return -1;
    }

    private static Breakpoint? FindByToken(Debugger debugger, string token)
    {
        if (!token.StartsWith("bp", StringComparison.Ordinal) || !int.TryParse(token[2..], out var n) || n < 1)
            return null;
        var index = 0;
        foreach (Breakpoint bp in debugger.Breakpoints)
        {
            if (++index == n) return bp;
        }
        return null;
    }

    private static string TokenOf(Breakpoint target, Debugger debugger) => $"bp{IndexOf(debugger, target)}";

    private static BreakpointInfo SnapshotBreakpoint(Breakpoint bp, int index)
        // Bind state is eventual and invisible to automation: a successful Add means accepted.
        => new($"bp{index}", bp.File, bp.FileLine, Bound: true, Warning: null);

    private static StackFrame? FindFrame(Debugger debugger, string frameId)
    {
        if (!frameId.StartsWith('f') || !int.TryParse(frameId[1..], out var n) || n < 0)
            return null;
        var index = 0;
        foreach (StackFrame frame in debugger.CurrentThread.StackFrames)
        {
            if (index++ == n) return frame;
        }
        return null;
    }

    private const int MaxValueLength = 500;

    private ValueInfo SnapshotValue(Expression e, string? path)
    {
        var raw = SafeGet(() => e.Value) ?? "(unavailable)";
        var truncated = raw.Length > MaxValueLength;
        return new ValueInfo(e.Name, SafeGet(() => e.Type) ?? "(unknown)",
            truncated ? raw[..MaxValueLength] + " [truncated]" : raw,
            truncated || HasMembers(e), path);
    }

    private EvaluateResult SnapshotEvaluation(Expression e, string expression, string mode, string? lastHit)
    {
        var raw = SafeGet(() => e.Value) ?? "(unavailable)";
        var truncated = raw.Length > MaxValueLength;
        return new EvaluateResult(expression,
            truncated ? raw[..MaxValueLength] + " [truncated]" : raw,
            SafeGet(() => e.Type) ?? "(unknown)", truncated || HasMembers(e), mode, lastHit);
    }

    private List<ValueInfo> SnapshotMembers(Expression parent, string path, int max)
    {
        var items = new List<ValueInfo>();
        try
        {
            foreach (Expression child in parent.DataMembers)
            {
                if (items.Count >= max) break;
                items.Add(SnapshotValue(child, $"{path}.{child.Name}"));
            }
        }
        catch (COMException)
        {
            // Member enumeration is best-effort; return what we have.
        }
        return items;
    }

    private static bool HasMembers(Expression e)
    {
        try { return e.DataMembers.Count > 0; }
        catch (COMException) { return false; }
    }

    private string? SafeGet(Func<string> read)
    {
        try { return read(); }
        catch (COMException ex)
        {
            TraceFallback(ex, "expression property read failed; using fallback text");
            return null;
        }
    }

    public async Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct)
    {
        List<string> names;
        try
        {
            names = await _dispatcher.InvokeAsync(EnumRotDteNames, 10_000, ct);
        }
        catch (TimeoutException)
        {
            throw new DebugDomainException(
                "timeout", "listing VS instances exceeded its budget", "dismiss any modal dialog in VS and retry");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }
        var items = new List<VsInstanceInfo>();
        var ordered = VsDiscovery.OrderForListing(names);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        for (var i = 0; i < ordered.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
            {
                foreach (var remaining in ordered.Skip(i))
                    items.Add(UnreachableInstance(remaining, "timeout: overall list budget exceeded; retry with fewer instances"));
                break;
            }
            var name = ordered[i];
            try
            {
                items.Add(await _dispatcher.InvokeAsync(() => SnapshotInstance(name), 5_000, ct));
            }
            catch (TimeoutException)
            {
                items.Add(UnreachableInstance(name, "timeout: VS instance busy or modal dialog open"));
            }
            catch (COMException ex)
            {
                items.Add(UnreachableInstance(name, ComErrorMapper.ToDomain(ex).Message));
            }
            catch (DebugDomainException ex)
            {
                items.Add(UnreachableInstance(name, $"{ex.Code}: {ex.Message}"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                items.Add(UnreachableInstance(name, $"automation-failed: {ex.Message}"));
            }
        }
        return new ListInstancesResult(items);
    }

    public Task<ListProcessesResult> ListProcessesAsync(string? nameFilter, bool ownedOnly, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        System.Diagnostics.Process[] all;
        try
        {
            all = System.Diagnostics.Process.GetProcesses();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new DebugDomainException(
                "automation-failed", $"could not enumerate local processes: {ex.Message}",
                "retry; if it persists, check the MCP host can read the process table");
        }
        var me = ProcessOwner.CurrentUserName();
        // One WMI query for all owners (2s cache) instead of one query per process.
        var owners = ProcessOwner.SnapshotOwners();
        var snapshot = new List<TargetProcessInfo>(all.Length);
        foreach (var proc in all)
        {
            ct.ThrowIfCancellationRequested();
            int pid;
            string name;
            try
            {
                pid = proc.Id;
                name = proc.ProcessName;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                continue; // exited mid-scan; skip, keep the rest
            }
            finally
            {
                try { proc.Dispose(); } catch { /* best effort */ }
            }
            var owner = owners.TryGetValue(pid, out var o) ? o : null;
            // Unknown owner fails closed: not mine, hidden under the owned-only default,
            // flagged with error text when the filter is off.
            var mine = owner is not null && owner.Equals(me, StringComparison.OrdinalIgnoreCase);
            snapshot.Add(new TargetProcessInfo(pid, name, owner ?? "", mine,
                owner is null ? "owner unknown: WMI lookup failed; failing closed" : null));
        }
        return Task.FromResult(new ListProcessesResult(
            ProcessDiscovery.FilterAndOrder(snapshot, nameFilter, ownedOnly)));
    }

    private static DTE BindRunningInstance(string? instanceOverride)
    {
        var picked = VsDiscovery.SelectCandidate(EnumRotNames(null), instanceOverride);
        if (picked is null)
            throw instanceOverride is null
                ? new DebugDomainException("vs-not-running", "no running VS instance found in the ROT",
                    "start Visual Studio 2026 and retry")
                : new DebugDomainException("vs-not-running", $"no running VS instance matching {instanceOverride}",
                    "check the instance override or start the matching VS and retry");
        var obj = GetRotObject(picked);
        if (obj is null)
            throw new DebugDomainException("vs-not-running", $"VS instance {picked} left the ROT",
                "start Visual Studio 2026 and retry");
        if (obj is not DTE dte)
            throw new DebugDomainException("automation-failed", $"ROT entry {picked} is not a VS automation object",
                "restart VS and retry");
        return dte;
    }

    private static List<string> EnumRotDteNames() => EnumRotNames(VsDiscovery.IsDteEntry);

    /// <summary>Single ROT-enumeration seam: runs <paramref name="body"/> with the
    /// table, bind context, and enumerator alive; releases all three on the way out.</summary>
    private static T WithRot<T>(Func<IRunningObjectTable, IBindCtx, IEnumMoniker, T> body)
    {
        IRunningObjectTable? rot = null;
        IEnumMoniker? enumMoniker = null;
        IBindCtx? ctx = null;
        try
        {
            Marshal.ThrowExceptionForHR(NativeMethods.GetRunningObjectTable(0, out rot));
            Marshal.ThrowExceptionForHR(NativeMethods.CreateBindCtx(0, out ctx));
            rot!.EnumRunning(out enumMoniker);
            return body(rot, ctx!, enumMoniker!);
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
        }
        finally
        {
            if (enumMoniker is not null) Marshal.ReleaseComObject(enumMoniker);
            if (ctx is not null) Marshal.ReleaseComObject(ctx);
            if (rot is not null) Marshal.ReleaseComObject(rot);
        }
    }

    /// <summary>Display names of every ROT entry passing <paramref name="accept"/>
    /// (all entries when null). Each fetched moniker is released immediately;
    /// only plain strings escape.</summary>
    private static List<string> EnumRotNames(Func<string, bool>? accept) =>
        WithRot((rot, ctx, enumMoniker) =>
        {
            var names = new List<string>();
            var monikers = new IMoniker[1];
            while (enumMoniker.Next(1, monikers, nint.Zero) == 0)
            {
                try
                {
                    monikers[0].GetDisplayName(ctx, null, out var name);
                    if (accept is null || accept(name))
                        names.Add(name);
                }
                catch (COMException) { /* skip unreadable entries */ }
                finally
                {
                    Marshal.ReleaseComObject(monikers[0]);
                }
            }
            return names;
        });

    /// <summary>Binds the ROT object for one display name (null when it left the ROT).
    /// Non-matching monikers are released during the scan, the match right after GetObject.</summary>
    private static object? GetRotObject(string displayName) =>
        WithRot((rot, ctx, enumMoniker) =>
        {
            var monikers = new IMoniker[1];
            while (enumMoniker.Next(1, monikers, nint.Zero) == 0)
            {
                var moniker = monikers[0];
                try
                {
                    moniker.GetDisplayName(ctx, null, out var name);
                    if (name != displayName) continue;
                    rot.GetObject(moniker, out var obj);
                    return obj;
                }
                catch (COMException) { /* skip unreadable entries */ }
                finally
                {
                    Marshal.ReleaseComObject(moniker);
                }
            }
            return null;
        });

    private static VsInstanceInfo UnreachableInstance(string displayName, string error)
        => new(displayName, VsDiscovery.TryParsePid(displayName),
            VsDiscovery.VersionTextOf(displayName), "", "", [], false, error);

    private VsInstanceInfo SnapshotInstance(string displayName)
    {
        var pid = VsDiscovery.TryParsePid(displayName);
        var version = VsDiscovery.VersionTextOf(displayName);
        DTE? dte = null;
        try
        {
            if (GetRotObject(displayName) is not DTE found)
                return UnreachableInstance(displayName, "vs-not-running: instance left the ROT");
            dte = found;
            string solutionPath;
            try { solutionPath = dte.Solution?.FullName ?? ""; }
            catch (COMException) { return UnreachableInstance(displayName, "automation-failed: solution unreadable"); }
            solutionPath ??= "";
            string solutionName;
            try { solutionName = solutionPath.Length == 0 ? "" : (Path.GetFileNameWithoutExtension(solutionPath) ?? ""); }
            catch (Exception) { solutionName = ""; }
            var projects = new List<string>();
            try
            {
                var solutionProjects = dte.Solution?.Projects;
                if (solutionProjects is not null)
                    CollectProjects(solutionProjects, projects);
            }
            catch (COMException) { /* keep what we have; still reachable */ }
            var capped = projects
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Take(50).ToList();
            return new VsInstanceInfo(displayName, pid, version, solutionName, solutionPath, capped, true, null);
        }
        catch (COMException ex)
        {
            var mapped = ComErrorMapper.ToDomain(ex);
            return new VsInstanceInfo(displayName, pid, version, "", "", [], false, mapped.Message);
        }
        catch (DebugDomainException ex)
        {
            return UnreachableInstance(displayName, $"{ex.Code}: {ex.Message}");
        }
        finally
        {
            if (dte is not null) { try { Marshal.ReleaseComObject(dte); } catch { /* best effort */ } }
        }
    }

    private const string SolutionFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

    private void CollectProjects(EnvDTE.Projects projects, List<string> projectNames)
    {
        foreach (Project project in projects)
        {
            try { CollectProject(project, projectNames); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceFallback(ex, "skipping unreadable project during listing");
            }
        }
    }

    private void CollectProject(Project project, List<string> projectNames)
    {
        string kind;
        try { kind = project.Kind ?? ""; }
        catch (COMException ex)
        {
            TraceFallback(ex, "skipping project with unreadable kind");
            return;
        }
        if (kind.Equals(SolutionFolderKind, StringComparison.OrdinalIgnoreCase))
        {
            ProjectItems? items;
            try { items = project.ProjectItems; }
            catch (COMException ex)
            {
                TraceFallback(ex, "skipping solution folder with unreadable items");
                return;
            }
            if (items is null) return;
            foreach (ProjectItem item in items)
            {
                try
                {
                    var sub = item.SubProject;
                    if (sub is not null) CollectProject(sub, projectNames);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    TraceFallback(ex, "skipping unreadable solution-folder child");
                }
            }
            return;
        }
        try
        {
            var name = project.Name;
            if (!string.IsNullOrWhiteSpace(name)) projectNames.Add(name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TraceFallback(ex, "skipping project with unreadable name");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_dte is not null)
        {
            try { Marshal.ReleaseComObject(_dte); } catch { /* best effort */ }
            _dte = null;
        }
        _dispatcher.Dispose();
    }

    /// <summary>Trace log for every silent fallback (CA1848: LoggerMessage delegate, no formatting cost when disabled).</summary>
    private void TraceFallback(Exception ex, string reason)
    {
        if (_logger is { } logger) LogSilentFallback(logger, ex, reason);
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Silent fallback: {reason}")]
    private static partial void LogSilentFallback(ILogger logger, Exception ex, string reason);

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport("ole32.dll")]
        internal static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
    }
}
