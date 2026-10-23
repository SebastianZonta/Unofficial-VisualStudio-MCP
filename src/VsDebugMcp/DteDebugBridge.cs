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
    private readonly object _gate = new();
    private readonly Dictionary<string, SessionEntry> _sessions = new();
    private bool _disposed;

    /// <summary>One live binding: a VS instance held by exactly one debug session.
    /// Dte is null once the instance is known dead; the record stays until detach.</summary>
    private sealed class SessionEntry
    {
        public DTE? Dte { get; set; }
        public string DisplayName { get; set; } = "";
        public int Pid { get; set; }
        public bool Dead { get; set; }
        /// <summary>Breakpoints this session created: deleted on detach so no stale
        /// conditional breakpoint surprises a later session. Touched on the dispatcher only.</summary>
        public List<Breakpoint> OwnedBreakpoints { get; } = new();
        /// <summary>Logpoints are emulated evaluate-then-decide (native log output lands in
        /// the VS Output window, which automation cannot read), so the Resume loop needs
        /// the template per file:line. Touched on the dispatcher only.</summary>
        public List<LogpointSpec> Logpoints { get; } = new();
        public List<BreakpointLogEntry> Log { get; } = new();
    }

    private sealed record LogpointSpec(string File, int Line, string LogMessage)
    {
        // File:line keying: two logpoints at the same location share the template match
        // (first wins per hit); remove clears every spec at the removed location.
        public string Key => $"{File}:{Line}";
    }

    public DteDebugBridge(ILogger<DteDebugBridge>? logger = null)
    {
        _logger = logger;
    }

    public async Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken cancellationToken)
    {
        var picked = await _dispatcher.InvokeAsync(
            () => VsDiscovery.SelectCandidate(EnumRotNames(null), instanceOverride), 10_000, cancellationToken);
        if (picked is null)
            throw instanceOverride is null
                ? new DebugDomainException("vs-not-running", "no running VS instance found in the ROT",
                    "start Visual Studio 2026 and retry")
                : new DebugDomainException("vs-not-running", $"no running VS instance matching {instanceOverride}",
                    "check the instance override or start the matching VS and retry");
        // Idempotent re-attach: the same display name returns its live session, never a duplicate binding.
        var existing = FindByDisplayName(picked);
        if (existing is not null)
        {
            var alive = await _dispatcher.InvokeAsync(() => ProbeAlive(existing.Value.Entry.Dte), 10_000, cancellationToken);
            if (alive)
            {
                var (mode, lastHit) = await ReadStateAsync(existing.Value.Id, cancellationToken);
                return new AttachResult(existing.Value.Id, mode, lastHit);
            }
            await DetachAsync(existing.Value.Id, cancellationToken);
        }
        var dte = await _dispatcher.InvokeAsync(() => BindDisplayName(picked), 10_000, cancellationToken);
        var sessionId = Guid.NewGuid().ToString("N");
        lock (_gate)
        {
            _sessions[sessionId] = new SessionEntry
            {
                Dte = dte,
                DisplayName = picked,
                Pid = VsDiscovery.TryParsePid(picked),
            };
        }
        var (freshMode, freshHit) = await ReadStateAsync(sessionId, cancellationToken);

        return new AttachResult(sessionId, freshMode, freshHit);
    }

    public Task RequireSessionAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _ = SessionDte(sessionId);
        return Task.CompletedTask;
    }

    /// <summary>Resolves the live DTE for a session. Unknown id is no-session;
    /// a dead binding is per-session vs-not-running (siblings unaffected).</summary>
    private DTE SessionDte(string sessionId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
                throw new DebugDomainException("no-session", "no active session", "call attach first");
            if (entry.Dead || entry.Dte is null)
                throw new DebugDomainException(
                    "vs-not-running", $"the VS instance bound to session {sessionId} is gone", "call attach again");
            return entry.Dte;
        }
    }

    private (string Id, SessionEntry Entry)? FindByDisplayName(string displayName)
    {
        lock (_gate)
        {
            foreach (var kv in _sessions)
            {
                if (kv.Value.DisplayName == displayName)
                    return (kv.Key, kv.Value);
            }
            return null;
        }
    }

    private static bool ProbeAlive(DTE? dte)
    {
        if (dte is null) return false;
        try { _ = dte.Name; return true; }
        catch (COMException) { return false; }
    }

    /// <summary>Marks one session dead on a disconnect HResult (releasing its RCW
    /// on the dispatcher, never the caller thread) and reports per-session vs-not-running.</summary>
    private DebugDomainException SessionComError(string sessionId, DTE dte, COMException ex)
    {
        if (ComErrorMapper.IsDisconnected(ex))
        {
            MarkDead(sessionId, dte);
            return new DebugDomainException(
                "vs-not-running", $"the VS instance bound to session {sessionId} is gone", "call attach again");
        }
        return ComErrorMapper.ToDomain(ex);
    }

    private void MarkDead(string sessionId, DTE dte)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry) && ReferenceEquals(entry.Dte, dte))
            {
                entry.Dte = null;
                entry.Dead = true;
            }
        }
        ReleaseOnDispatcher(dte);
    }

    private void ReleaseOnDispatcher(DTE dte)
    {
        try
        {
            _ = _dispatcher.InvokeAsync(() =>
            {
                try { Marshal.ReleaseComObject(dte); } catch { /* best effort */ }
                return true;
            }, CancellationToken.None);
        }
        catch { /* dispatcher gone; nothing left to release through */ }
    }

    public Task<ListSessionsResult> ListSessionsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(new ListSessionsResult(
                _sessions.Select(kv => new SessionInfo(kv.Key, kv.Value.DisplayName)).ToList()));
        }
    }

    public async Task<DetachResult> DetachAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SessionEntry entry;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var found))
                throw new DebugDomainException("no-session", "no active session", "call attach first");
            _sessions.Remove(sessionId);
            entry = found;
        }
        // Detach removes the record even for dead bindings; breakpoint cleanup and
        // the release are best-effort. Deleting this session's breakpoints first
        // keeps stale conditional breakpoints out of later sessions on the same VS.
        var owned = entry.OwnedBreakpoints.ToList();
        if (entry.Dte is { } dte)
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    foreach (var bp in owned)
                    {
                        try { bp.Delete(); } catch { /* best effort */ }
                        try { Marshal.ReleaseComObject(bp); } catch { /* best effort */ }
                    }
                    try { Marshal.ReleaseComObject(dte); } catch { /* best effort */ }
                    return true;
                }, 10_000, ct);
            }
            catch (Exception ex) when (ex is TimeoutException or COMException or OperationCanceledException)
            {
                // Session already forgotten; cleanup was best-effort.
            }
        }
        return new DetachResult(sessionId);
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
        var dte = SessionDte(sessionId);
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(dte);
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
            throw SessionComError(sessionId, dte, ex);
        }
    }

    // shortcut: CoreCLR engine only (.NET 5+ targets), add an engine parameter when native/.NET Framework launch is needed.
    private const string CoreClrEngine = "{2E36F1D4-B23C-435D-AB41-18E608940038}";

    public async Task<LaunchProcessResult> LaunchProcessAsync(string sessionId, string executablePath, string? arguments,
        string? workingDirectory, IReadOnlyDictionary<string, string>? environment, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        var cwd = ProcessLaunch.CheckStart(executablePath, workingDirectory);
        var envBlock = ProcessLaunch.BuildEnvironmentBlock(ProcessLaunch.InheritedEnvironment(), environment);
        var dte = SessionDte(sessionId);
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        // Set when the caller stops waiting: the STA work may still be running and must
        // then end its target process, never leave it bound behind a reported failure.
        var abandoned = 0;
        int pid;
        try
        {
            pid = await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(dte);
                if (debugger.DebuggedProcesses.Count > 0)
                    throw ProcessLaunch.AlreadyBound(debugger.DebuggedProcesses.Item(1).ProcessID);
                // Attached from birth: start suspended, attach the managed engine, then let
                // user code run, so breakpoints set before launch bind at startup. The bridge
                // creates the process as its own user, so it is mine by construction.
                using var started = ProcessLaunch.StartSuspended(executablePath, arguments, cwd, envBlock);
                try
                {
                    EnvDTE80.Process2? target = null;
                    foreach (EnvDTE80.Process2 candidate in debugger.LocalProcesses)
                    {
                        if (candidate.ProcessID == started.Pid) { target = candidate; break; }
                    }
                    if (target is null)
                        throw new DebugDomainException(
                            ErrorCodes.LaunchFailed, $"started target PID {started.Pid} is not visible to the VS instance",
                            "retry the launch");
                    target.Attach2(CoreClrEngine);
                    if (Volatile.Read(ref abandoned) != 0)
                        throw new OperationCanceledException("launch abandoned by the caller");
                    started.Resume();
                }
                catch
                {
                    started.Kill();
                    throw;
                }
                return started.Pid;
            }, timeoutMs, ct);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException && Abandon(ref abandoned))
        {
            if (ex is OperationCanceledException)
                throw;
            throw new DebugDomainException(
                ErrorCodes.Timeout, $"launching {executablePath} exceeded timeoutMs and was abandoned (an unfinished start is ended, never left bound)",
                "dismiss any VS dialog, then retry with a larger timeoutMs");
        }
        catch (COMException ex)
        {
            throw SessionComError(sessionId, dte, ex);
        }
        // Same wait as resume: an entry-point hit (or logpoint) surfaces here, still-running is ok.
        var run = await PollResumeAsync(sessionId, deadline, timeoutMs, 0, ct);
        return new LaunchProcessResult(pid, run.Mode, run.LastHit, run.Status, $"launched target process {pid}; {run.Note}");
    }

    private static bool Abandon(ref int flag)
    {
        Volatile.Write(ref flag, 1);
        return true;
    }

    public async Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct,
        string? condition = null, int? hitCount = null, string? hitCountMode = null, string? logMessage = null)
    {
        // Validation first: no debugger touched, nothing created on failure.
        var error = BreakpointOptions.Check(condition, hitCount, hitCountMode, logMessage);
        if (error is not null)
            throw new DebugDomainException(ErrorCodes.InvalidArgument, error.Value.Detail, error.Value.Remediation);
        var (cond, count, mode, log) = BreakpointOptions.Normalize(condition, hitCount, hitCountMode, logMessage);
        // Conditions map natively (break-when-true); hit counts map natively
        // (equal = exact Nth, multiple = every Nth). Logpoints stay stopping at
        // the VS level and are emulated evaluate-then-decide by the Resume loop,
        // because native log output (VS Output window) is not automation-readable.
        var added = await RunSnapshotAsync(sessionId, debugger =>
        {
            debugger.Breakpoints.Add(
                Function: "", File: file, Line: line, Column: 1,
                Condition: cond ?? "", ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue,
                Language: "",
                Data: "", DataCount: 0, Address: "", HitCount: count ?? 0,
                HitCountType: count is null ? dbgHitCountType.dbgHitCountTypeNone
                    : mode == HitCountModes.Every ? dbgHitCountType.dbgHitCountTypeMultiple
                    : dbgHitCountType.dbgHitCountTypeEqual);
            // Add returns the collection; the just-added breakpoint is the last item.
            var bp = debugger.Breakpoints.Item(debugger.Breakpoints.Count);
            var info = SnapshotBreakpoint(bp, debugger.Breakpoints.Count);
            // Automation cannot observe pending-vs-bound (Concord-only); a successful Add means VS accepted it.
            return (Breakpoint: bp, Info: info with { Bound = true }, Mode: ModeOf(debugger), LastHit: LastHitOf(debugger));
        }, ct);
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry) && !entry.Dead && entry.Dte is not null)
            {
                entry.OwnedBreakpoints.Add(added.Breakpoint);
                if (log is not null)
                    // Key on the VS snapshot path (full path), never the user-passed file:
                    // the Resume loop matches specs against lastHit, which is full-path.
                    entry.Logpoints.Add(new LogpointSpec(added.Info.File, added.Info.Line, log));
            }
        }
        // Round-trip the logpoint template on set: the VS-side log action is not
        // automation-readable, so the session spec is the source for LogMessage.
        return new SetBreakpointResult(added.Info with { LogMessage = log }, added.Mode, added.LastHit);
    }

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            List<LogpointSpec> specs;
            List<BreakpointLogEntry> log;
            lock (_gate)
            {
                specs = _sessions.TryGetValue(sessionId, out var entry)
                    ? entry.Logpoints.ToList() : new List<LogpointSpec>();
                log = _sessions.TryGetValue(sessionId, out var found)
                    ? found.Log.ToList() : new List<BreakpointLogEntry>();
            }
            var items = new List<BreakpointInfo>();
            var index = 0;
            foreach (Breakpoint bp in debugger.Breakpoints)
            {
                index++;
                var info = SnapshotBreakpoint(bp, index);
                var spec = specs.FirstOrDefault(s =>
                    s.File.Equals(info.File, StringComparison.OrdinalIgnoreCase) && s.Line == info.Line);
                items.Add(spec is null ? info : info with { LogMessage = spec.LogMessage });
            }
            return new ListBreakpointsResult(items, ModeOf(debugger), LastHitOf(debugger), log);
        }, ct);

    public async Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct)
        => await RunSnapshotAsync(sessionId, debugger =>
        {
            var target = FindByToken(debugger, breakpointId);
            if (target is null)
                throw new DebugDomainException(
                    "breakpoint-not-found", $"no breakpoint {breakpointId}", "call list_breakpoints for valid ids");
            var id = TokenOf(target, debugger);
            var file = target.File;
            var line = target.FileLine;
            target.Delete();
            lock (_gate)
            {
                if (_sessions.TryGetValue(sessionId, out var entry))
                {
                    entry.OwnedBreakpoints.RemoveAll(b => ReferenceEquals(b, target));
                    entry.Logpoints.RemoveAll(s =>
                        s.File.Equals(file, StringComparison.OrdinalIgnoreCase) && s.Line == line);
                }
            }
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
        var pauseDte = SessionDte(sessionId);
        try
        {
            // Non-blocking break: Break(true) would park the dispatcher until break
            // mode is reached; poll instead so a timeout never wedges later calls.
            await _dispatcher.InvokeAsync(() => { DebuggerOf(pauseDte).Break(false); return true; }, 10_000, ct);
        }
        catch (TimeoutException)
        {
            return new ExecutionResult("run", null, "timeout", "pause request timed out while issuing; retry");
        }
        catch (COMException ex)
        {
            throw SessionComError(sessionId, pauseDte, ex);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                return new ExecutionResult("run", null, "timeout", "pause request issued but break mode not reached in time");
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            await RequireSessionAsync(sessionId, ct);
            var (mode, lastHit) = await TrySnapshotAsync(sessionId, ct);
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
        var resumeDte = SessionDte(sessionId);
        if (currentMode == "break")
        {
            // A hit already pending may be a logpoint: log-and-continue it instead
            // of blind-resuming past it (which would silently drop the log entry).
            var (preMode, preHit) = await TrySnapshotAsync(sessionId, ct);
            if (preMode == "break" && await TryLogAndContinueAsync(sessionId, preHit, timeoutMs, ct))
                return await PollResumeAsync(sessionId, DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs), timeoutMs, 1, ct);
        }
        try
        {
            // Non-blocking Go: returns immediately after resuming so the single STA
            // dispatcher thread is never held hostage by a long-running debuggee.
            // Completion (next breakpoint or process exit) is observed by polling below.
            await _dispatcher.InvokeAsync(() => { DebuggerOf(resumeDte).Go(false); return true; }, IssueBudgetMs(timeoutMs), ct);
        }
        catch (TimeoutException)
        {
            // The Go issue may or may not have taken effect: re-snapshot instead of
            // assuming. Running plus a free dispatcher is success, not a timeout.
            var (issueMode, issueHit) = await TrySnapshotAsync(sessionId, ct);
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
            throw SessionComError(sessionId, resumeDte, ex);
        }

        return await PollResumeAsync(sessionId, DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs), timeoutMs, 0, ct);
    }

    private async Task<ExecutionResult> PollResumeAsync(string sessionId, DateTime deadline, int timeoutMs, int logged, CancellationToken ct)
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
                var (finalMode, finalHit) = await TrySnapshotAsync(sessionId, ct);
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
            var (mode, lastHit) = await TrySnapshotAsync(sessionId, ct);
            if (mode == "run")
                seenRun = true;
            if (mode == "break")
            {
                // Emulated logpoint: buffer the formatted template and keep going;
                // a logpoint hit never surfaces as a stop.
                if (await TryLogAndContinueAsync(sessionId, lastHit, timeoutMs, ct))
                {
                    logged++;
                    continue;
                }
                return new ExecutionResult(mode, lastHit, "ok",
                    logged > 0 ? $"hit breakpoint; logged {logged} logpoint hits" : "hit breakpoint");
            }
            if (mode == "design")
                return new ExecutionResult(mode, null, "ok", "run ended back in design mode");
        }
    }

    /// <summary>Emulated logpoint hit: formats the template against the live frame,
    /// buffers it session-side, and resumes the debuggee. Returns false for stopping
    /// hits (unknown location, evaluation unavailable), which surface normally.</summary>
    private async Task<bool> TryLogAndContinueAsync(string sessionId, string? lastHit, int timeoutMs, CancellationToken ct)
    {
        if (lastHit is null)
            return false;
        LogpointSpec? spec;
        lock (_gate)
        {
            spec = _sessions.TryGetValue(sessionId, out var entry)
                ? entry.Logpoints.FirstOrDefault(s => s.Key.Equals(lastHit, StringComparison.OrdinalIgnoreCase))
                : null;
        }
        if (spec is null)
            return false;
        var captured = spec;
        (string Token, string Message, int Hits, int ThreadId)? logged;
        try
        {
            logged = await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(SessionDte(sessionId));
                if (ModeOf(debugger) != "break")
                    return ((string, string, int, int)?)null;
                foreach (Breakpoint bp in debugger.Breakpoints)
                {
                    if ($"{bp.File}:{bp.FileLine}".Equals(lastHit, StringComparison.OrdinalIgnoreCase))
                    {
                        var message = BreakpointOptions.FormatLogMessage(captured.LogMessage, expr =>
                        {
                            try
                            {
                                var evaluated = debugger.GetExpression(expr, false, 5000);
                                return evaluated.IsValidValue ? evaluated.Value ?? "(unavailable)" : "(unavailable)";
                            }
                            catch (COMException)
                            {
                                return "(unavailable)";
                            }
                        });
                        var tid = -1;
                        try { tid = debugger.CurrentThread?.ID ?? -1; } catch (COMException) { }
                        return ((string, string, int, int)?)(TokenOf(bp, debugger), message, HitsOf(bp), tid);
                    }
                }
                return ((string, string, int, int)?)null;
            }, 10_000, ct);
        }
        catch (Exception ex) when (ex is TimeoutException or COMException or OperationCanceledException)
        {
            return false;
        }
        if (logged is null)
            return false;
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
                BreakpointOptions.AppendLog(entry.Log,
                    new BreakpointLogEntry(logged.Value.Token, logged.Value.Message, logged.Value.Hits));
        }
        var contDte = SessionDte(sessionId);
        try
        {
            await _dispatcher.InvokeAsync(() => { DebuggerOf(contDte).Go(false); return true; }, IssueBudgetMs(timeoutMs), ct);
        }
        catch (COMException ex)
        {
            throw SessionComError(sessionId, contDte, ex);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            // The Go may still have taken effect; keep polling within the deadline.
        }
        // The Go is non-blocking: the mode still reads break until the resumed
        // thread actually runs, and the poll loop would re-log the same hit.
        // Settle (bounded) until the resume takes effect or another thread's hit
        // surfaces; any later break is then genuinely new.
        var settled = System.Diagnostics.Stopwatch.StartNew();
        while (settled.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(100, ct);
            var (settleMode, _, settleThread) = await TrySnapshotThreadAsync(sessionId, ct);
            if (settleMode != "break" || settleThread != logged.Value.ThreadId)
                break;
        }
        return true;
    }

    private async Task<(string Mode, string? LastHit, int ThreadId)> TrySnapshotThreadAsync(string sessionId, CancellationToken ct)
    {
        var dte = SessionDte(sessionId);
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(dte);
                var mode = ModeOf(debugger);
                if (mode != "break")
                    return (mode, (string?)null, -1);
                var tid = -1;
                try { tid = debugger.CurrentThread?.ID ?? -1; } catch (COMException) { }
                return (mode, LastHitOf(debugger), tid);
            }, 10_000, ct);
        }
        catch (TimeoutException)
        {
            return ("unknown", null, -1);
        }
        catch (COMException)
        {
            return ("unknown", null, -1);
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
        var stepDte = SessionDte(sessionId);
        try
        {
            // Non-blocking step for the same reason as Go(false): never park the
            // dispatcher inside a potentially long debugger wait.
            await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(stepDte);
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
            var (issueMode, issueHit) = await TrySnapshotAsync(sessionId, ct);
            var mode = issueMode ?? "unknown";
            return new ExecutionResult(mode, issueMode == "break" ? issueHit : null, "timeout",
                issueMode is null
                    ? "step request timed out while issuing; debugger state unreadable, retry or pause"
                    : $"step request timed out while issuing; debugger mode is {mode}, retry or pause");
        }
        catch (COMException ex)
        {
            throw SessionComError(sessionId, stepDte, ex);
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
                var (finalMode, finalHit) = await TrySnapshotAsync(sessionId, ct);
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
            var (mode, _) = await TrySnapshotAsync(sessionId, ct);
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
        var dte = SessionDte(sessionId);
        try
        {
            return await _dispatcher.InvokeAsync(() => work(DebuggerOf(dte)), 30_000, ct);
        }
        catch (TimeoutException)
        {
            throw new DebugDomainException(
                "timeout", "VS automation call exceeded its budget", "dismiss any modal dialog in VS and retry");
        }
        catch (COMException ex)
        {
            throw SessionComError(sessionId, dte, ex);
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

    private async Task<(string Mode, string? LastHit)> ReadStateAsync(string sessionId, CancellationToken ct)
    {
        var dte = SessionDte(sessionId);
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(dte);
                return (ModeOf(debugger), LastHitOf(debugger));
            }, 10_000, ct);
        }
        catch (TimeoutException)
        {
            return ("design", null);
        }
        catch (COMException ex)
        {
            if (ComErrorMapper.IsDisconnected(ex))
                MarkDead(sessionId, dte);
            return ("design", null);
        }
    }

    /// <summary>Fast strict snapshot for pre-flight mode checks; throws on timeout/COM.</summary>
    private async Task<string> SnapshotModeAsync(DTE dte, CancellationToken ct)
    {
        return await _dispatcher.InvokeAsync(() => ModeOf(DebuggerOf(dte)), 10_000, ct);
    }

    /// <summary>Shared Pause/Resume/Step pre-flight: session check plus strict mode read.
    /// A mode-read timeout throws <see cref="ModeReadTimeoutException"/> carrying the
    /// op-specific timeout result, which the caller returns verbatim.</summary>
    private async Task<string> RequireModeAsync(string sessionId, ExecutionResult timeoutResult, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        var dte = SessionDte(sessionId);
        try
        {
            return await SnapshotModeAsync(dte, ct);
        }
        catch (TimeoutException)
        {
            throw new ModeReadTimeoutException(timeoutResult);
        }
        catch (COMException ex)
        {
            throw SessionComError(sessionId, dte, ex);
        }
    }

    private sealed class ModeReadTimeoutException(ExecutionResult timeoutResult) : Exception
    {
        public ExecutionResult TimeoutResult { get; } = timeoutResult;
    }

    /// <summary>Best-effort snapshot for polling: null mode means "keep waiting".</summary>
    private async Task<(string? Mode, string? LastHit)> TrySnapshotAsync(string sessionId, CancellationToken ct)
    {
        var dte = SessionDte(sessionId);
        try
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                var debugger = DebuggerOf(dte);
                return ((string?)ModeOf(debugger), LastHitOf(debugger));
            }, 5_000, ct);
        }
        catch (COMException ex)
        {
            if (ComErrorMapper.IsDisconnected(ex))
                MarkDead(sessionId, dte);
            TraceFallback(ex, "transient debugger snapshot failed; polling continues");
            return (null, null);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
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

    // Hits accrue on the bound children; the pending parent always reports 0.
    private static int HitsOf(Breakpoint bp)
    {
        var hits = 0;
        foreach (Breakpoint child in bp.Children) hits += child.CurrentHits;
        return hits;
    }

    private static BreakpointInfo SnapshotBreakpoint(Breakpoint bp, int index)
    {
        // Bind state is eventual and invisible to automation: a successful Add means accepted.
        // Option reads are best-effort; a transient COM failure falls back to the plain shape.
        string? condition = null;
        int? target = null;
        string? mode = null;
        var hits = 0;
        try
        {
            condition = string.IsNullOrWhiteSpace(bp.Condition) ? null : bp.Condition;
            if (bp.HitCountType != dbgHitCountType.dbgHitCountTypeNone)
            {
                target = bp.HitCountTarget;
                mode = bp.HitCountType == dbgHitCountType.dbgHitCountTypeMultiple
                    ? HitCountModes.Every : HitCountModes.Exact;
            }
            hits = HitsOf(bp);
        }
        catch (COMException)
        {
            // Keep the plain shape; options stay null/zero rather than failing the list.
        }
        // LogMessage comes from the session's logpoint specs (see ListBreakpointsAsync);
        // the VS-side log action is not automation-readable.
        return new($"bp{index}", bp.File, bp.FileLine, Bound: true, Warning: null,
            condition, target, mode, hits, LogMessage: null);
    }

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

    public async Task<ListInstancesResult> ListInstancesAsync(CancellationToken cancellationToken)
    {
        List<string> names;
        try
        {
            names = await _dispatcher.InvokeAsync(EnumRotDteNames, 10_000, cancellationToken);
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
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
            {
                foreach (var remaining in ordered.Skip(i))
                    items.Add(UnreachableInstance(remaining, "timeout: overall list budget exceeded; retry with fewer instances"));
                break;
            }
            var name = ordered[i];
            try
            {
                items.Add(await _dispatcher.InvokeAsync(() => SnapshotInstance(name), 5_000, cancellationToken));
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

    /// <summary>Binds the already-selected ROT display name (null when it left the ROT).</summary>
    private static DTE BindDisplayName(string picked)
    {
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
            if (enumMoniker is not null) 
                Marshal.ReleaseComObject(enumMoniker);

            if (ctx is not null) 
                Marshal.ReleaseComObject(ctx);

            if (rot is not null) 
                Marshal.ReleaseComObject(rot);
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
        List<DTE> live;
        lock (_gate)
        {
            live = _sessions.Values
                .Where(e => e.Dte is not null)
                .Select(e => e.Dte!)
                .ToList();
            _sessions.Clear();
        }
        // One best-effort release-all serialized on the dispatcher (never the caller thread).
        if (live.Count > 0)
        {
            try
            {
                _ = _dispatcher.InvokeAsync(() =>
                {
                    foreach (var dte in live)
                    {
                        try { Marshal.ReleaseComObject(dte); } catch { /* best effort */ }
                    }
                    return true;
                }, CancellationToken.None);
            }
            catch { /* dispatcher gone; nothing left to release through */ }
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
