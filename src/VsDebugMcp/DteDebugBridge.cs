using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;

namespace VsDebugMcp;

/// <summary>Live bridge: EnvDTE automation against the ROT-discovered VS 2026 instance.
/// Every COM call runs on the single <see cref="ComDispatcher"/> STA thread; live handles
/// are snapshotted to plain DTOs inside the same call and never escape.</summary>
internal sealed class DteDebugBridge : IDebugBridge, IDisposable
{
    private readonly ComDispatcher _dispatcher = new();
    private DTE? _dte;
    private string? _activeSessionId;
    private bool _disposed;

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
        => await RunSnapshotAsync(sessionId, ct, debugger =>
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
        });

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, ct, debugger =>
        {
            var items = new List<BreakpointInfo>();
            var index = 0;
            foreach (Breakpoint bp in debugger.Breakpoints)
            {
                index++;
                items.Add(SnapshotBreakpoint(bp, index));
            }
            return new ListBreakpointsResult(items, ModeOf(debugger), LastHitOf(debugger));
        });

    public async Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, ct, debugger =>
        {
            var target = FindByToken(debugger, breakpointId);
            if (target is null)
                throw new DebugDomainException(
                    "breakpoint-not-found", $"no breakpoint {breakpointId}", "call list_breakpoints for valid ids");
            var id = TokenOf(target, debugger);
            target.Delete();
            return new RemoveBreakpointResult(id, ModeOf(debugger), LastHitOf(debugger));
        });

    public async Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        string currentMode;
        try
        {
            currentMode = await SnapshotModeAsync(ct);
        }
        catch (TimeoutException)
        {
            return new ExecutionResult("run", null, "timeout", "pause request timed out while reading debugger state; retry");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
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
        await RequireSessionAsync(sessionId, ct);
        string currentMode;
        try
        {
            currentMode = await SnapshotModeAsync(ct);
        }
        catch (TimeoutException)
        {
            return new ExecutionResult("run", null, "timeout", "resume request timed out while reading debugger state; retry");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
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
            return new ExecutionResult("run", null, "timeout", "resume request timed out while issuing Go; retry");
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
                return new ExecutionResult("run", null, "timeout", "resume exceeded timeoutMs; debuggee still running, dispatcher is free");
            await Task.Delay(ClampDelay(remaining), ct);
            await RequireSessionAsync(sessionId, ct);
            var (mode, lastHit) = await TrySnapshotAsync(ct);
            if (mode == "break")
                return new ExecutionResult(mode, lastHit, "ok", "hit breakpoint");
            if (mode == "design")
                return new ExecutionResult(mode, null, "ok", "run ended back in design mode");
        }
    }

    public async Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        string currentMode;
        try
        {
            currentMode = await SnapshotModeAsync(ct);
        }
        catch (TimeoutException)
        {
            return new ExecutionResult("break", null, "timeout", "step request timed out while reading debugger state; mode unchanged, retry or pause");
        }
        catch (COMException ex)
        {
            throw ComErrorMapper.ToDomain(ex);
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
            return new ExecutionResult("break", null, "timeout", "step request timed out while issuing; mode unchanged, retry or pause");
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
                return new ExecutionResult("break", null, "timeout", "step exceeded timeoutMs; mode unchanged, retry or pause");
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
        => await RunSnapshotAsync(sessionId, ct, debugger =>
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
        });

    public async Task<ReadVariablesResult> ReadVariablesAsync(
        string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, ct, debugger =>
        {
            RequireBreak(debugger);
            if (path is not null)
            {
                var evaluated = debugger.GetExpression(path, true, 5000);
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
        });

    public async Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, ct, debugger =>
        {
            RequireBreak(debugger);
            var evaluated = debugger.GetExpression(expression, true, timeoutMs);
            if (!evaluated.IsValidValue)
                throw new DebugDomainException(
                    "invalid-expression", $"expression parsed but did not evaluate: {expression}",
                    "pass a read-only expression valid in the current frame");
            return SnapshotEvaluation(evaluated, expression, ModeOf(debugger), LastHitOf(debugger));
        });

    // ---- helpers (all run on the dispatcher thread) ----

    private async Task<T> RunSnapshotAsync<T>(string sessionId, CancellationToken ct, Func<Debugger, T> work)
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
        return index;
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

    private static ValueInfo SnapshotValue(Expression e, string? path)
    {
        var raw = SafeGet(() => e.Value) ?? "(unavailable)";
        var truncated = raw.Length > MaxValueLength;
        return new ValueInfo(e.Name, SafeGet(() => e.Type) ?? "(unknown)",
            truncated ? raw[..MaxValueLength] + " [truncated]" : raw,
            truncated || HasMembers(e), path);
    }

    private static EvaluateResult SnapshotEvaluation(Expression e, string expression, string mode, string? lastHit)
    {
        var raw = SafeGet(() => e.Value) ?? "(unavailable)";
        var truncated = raw.Length > MaxValueLength;
        return new EvaluateResult(expression,
            truncated ? raw[..MaxValueLength] + " [truncated]" : raw,
            SafeGet(() => e.Type) ?? "(unknown)", truncated || HasMembers(e), mode, lastHit);
    }

    private static List<ValueInfo> SnapshotMembers(Expression parent, string path, int max)
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

    private static string? SafeGet(Func<string> read)
    {
        try { return read(); }
        catch (COMException) { return null; }
    }

    private static DTE BindRunningInstance(string? instanceOverride)
    {
        IRunningObjectTable? rot = null;
        IEnumMoniker? enumMoniker = null;
        IBindCtx? ctx = null;
        try
        {
            NativeMethods.GetRunningObjectTable(0, out rot);
            NativeMethods.CreateBindCtx(0, out ctx);
            rot!.EnumRunning(out enumMoniker);
            var monikers = new IMoniker[1];
            var names = new List<string>();
            var byName = new Dictionary<string, IMoniker>(StringComparer.Ordinal);
            while (enumMoniker!.Next(1, monikers, nint.Zero) == 0)
            {
                try
                {
                    monikers[0].GetDisplayName(ctx, null, out var name);
                    names.Add(name);
                    byName[name] = monikers[0];
                }
                catch (COMException) { /* skip unreadable entries */ }
            }
            var picked = VsDiscovery.SelectCandidate(names, instanceOverride);
            if (picked is null)
                throw instanceOverride is null
                    ? new DebugDomainException("vs-not-running", "no running VS instance found in the ROT",
                        "start Visual Studio 2026 and retry")
                    : new DebugDomainException("vs-not-running", $"no running VS instance matching {instanceOverride}",
                        "check the instance override or start the matching VS and retry");
            rot.GetObject(byName[picked], out var obj);
            if (obj is not DTE dte)
                throw new DebugDomainException("automation-failed", $"ROT entry {picked} is not a VS automation object",
                    "restart VS and retry");
            return dte;
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

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport("ole32.dll")]
        internal static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
    }
}
