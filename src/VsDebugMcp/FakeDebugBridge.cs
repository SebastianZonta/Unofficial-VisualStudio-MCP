namespace VsDebugMcp;

/// <summary>In-memory fake sessions backing the tickets. One session per instance key:
/// re-attaching the same key returns the live session, like the real bridge.</summary>
public sealed class FakeDebugBridge : IDebugBridge
{
    private sealed class FakeSession
    {
        public string InstanceKey { get; set; } = "";
        public string Mode { get; set; } = "design";
        public string? LastHit { get; set; }
        public Dictionary<string, FakeBreakpoint> Breakpoints { get; } = new();
        public List<BreakpointLogEntry> Log { get; } = new();
        public bool Dead { get; set; }
        /// <summary>Target PID bound via attach_process or launch_process; null while unbound.</summary>
        public int? TargetPid { get; set; }
    }

    private sealed record FakeBreakpoint(
        string File, int Line, string? Condition, int? HitCountTarget, string? HitCountMode,
        int HitCount, string? LogMessage)
    {
        public FakeBreakpoint WithHitCount(int hitCount) =>
            this with { HitCount = hitCount };
    }

    private readonly Dictionary<string, FakeSession> _sessions = new();
    private int _breakpointCounter;
    private int _pidCounter = 50_000;

    /// <summary>Known local processes and their owners (DOMAIN\user). Empty by default: every pid is unknown.</summary>
    public Dictionary<int, string> ProcessOwners { get; } = new();

    /// <summary>User the bridge runs as. Real bridge uses the OS identity; the fake lets tests pin it.</summary>
    public string CurrentUser { get; set; } = "FAKE\\dev";

    /// <summary>Fake for module symbol load. When false, new breakpoints stay unbound with a warning;
    /// flipping it back to true binds them on the next list (poll-to-confirm).</summary>
    public bool SymbolsLoaded { get; set; } = true;

    /// <summary>Fake for an over-budget wait. When true, resume/step return a timeout status without changing mode.</summary>
    public bool ForceTimeout { get; set; }

    /// <summary>Fake for a parsed-but-not-evaluable expression. When true, evaluate returns invalid-expression.</summary>
    public bool ForceInvalidExpression { get; set; }

    public List<VsInstanceInfo> Instances { get; } = new();

    /// <summary>Known local target processes. Empty by default: every pid is unknown.</summary>
    public List<TargetProcessInfo> Processes { get; } = new();

    public Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = instanceOverride ?? "default";
        foreach (var kv in _sessions)
        {
            if (!kv.Value.Dead && kv.Value.InstanceKey == key)
                return Task.FromResult(new AttachResult(kv.Key, kv.Value.Mode, kv.Value.LastHit));
        }
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = new FakeSession { InstanceKey = key };
        return Task.FromResult(new AttachResult(sessionId, "design", LastHit: null));
    }

    public Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ListInstancesResult(Instances.ToList()));
    }

    public Task<ListProcessesResult> ListProcessesAsync(string? nameFilter, bool ownedOnly, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ListProcessesResult(
            ProcessDiscovery.FilterAndOrder(Processes, nameFilter, ownedOnly)));
    }

    public async Task RequireSessionAsync(string sessionId, CancellationToken ct)
    {
        _ = await RequireAsync(sessionId, ct);
    }

    private Task<FakeSession> RequireAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new DebugDomainException("no-session", "no active session", "call attach first");
        if (session.Dead)
            throw new DebugDomainException(
                "vs-not-running", $"the VS instance bound to session {sessionId} is gone", "call attach again");
        return Task.FromResult(session);
    }

    /// <summary>Test hook: simulates the bound VS exiting mid-session.</summary>
    public void SimulateVsExit(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.Dead = true;
    }

    public Task<ListSessionsResult> ListSessionsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ListSessionsResult(
            _sessions.Select(kv => new SessionInfo(kv.Key, kv.Value.InstanceKey)).ToList()));
    }

    public async Task<DetachResult> DetachAsync(string sessionId, CancellationToken ct)
    {
        _ = await RequireAsync(sessionId, ct);
        _sessions.Remove(sessionId);
        return new DetachResult(sessionId);
    }

    public async Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct,
        string? condition = null, int? hitCount = null, string? hitCountMode = null, string? logMessage = null)
    {
        var session = await RequireAsync(sessionId, ct);
        var error = BreakpointOptions.Check(condition, hitCount, hitCountMode, logMessage);
        if (error is not null)
            throw new DebugDomainException(ErrorCodes.InvalidArgument, error.Value.Detail, error.Value.Remediation);
        var (cond, count, mode, log) = BreakpointOptions.Normalize(condition, hitCount, hitCountMode, logMessage);
        var id = $"bp{++_breakpointCounter}";
        session.Breakpoints[id] = new FakeBreakpoint(file, line, cond, count, mode, HitCount: 0, log);
        return new SetBreakpointResult(Render(id, session), session.Mode, session.LastHit);
    }

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        var items = session.Breakpoints.Keys.Select(bp => Render(bp, session)).ToList();
        return new ListBreakpointsResult(items, session.Mode, session.LastHit, session.Log.ToList());
    }

    public async Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        if (!session.Breakpoints.Remove(breakpointId))
            throw new DebugDomainException(
                "breakpoint-not-found", $"no breakpoint {breakpointId}", "call listsession.Breakpoints for valid ids");
        return new RemoveBreakpointResult(breakpointId, session.Mode, session.LastHit);
    }

    public async Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        if (session.Mode != "run")
            throw new DebugDomainException(
                "not-running", $"debuggee is {session.Mode}, nothing to pause", "resume first, then pause while running");
        session.Mode = "break";
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: "paused on request");
    }

    public async Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (session.Mode == "run")
            throw new DebugDomainException(
                "already-running", "debuggee is already running", "pause first to inspect");
        if (ForceTimeout)
            return new ExecutionResult(session.Mode, session.LastHit, Status: "timeout", Note: "resume exceeded timeoutMs; still waiting");
        session.Mode = "run";
        session.LastHit = null;
        // Evaluate-then-decide per hit, in creation order: the counter advances on
        // every evaluation pass; a logpoint appends and never stops; a stopping
        // breakpoint fires only when its hit-count mode and condition both agree.
        if (SymbolsLoaded)
        {
            foreach (var id in session.Breakpoints.Keys.OrderBy(BpIndex).ToList())
            {
                var bp = session.Breakpoints[id];
                session.Breakpoints[id] = bp.WithHitCount(bp.HitCount + 1);
                var updated = session.Breakpoints[id];
                if (updated.LogMessage is not null)
                {
                    BreakpointOptions.AppendLog(session.Log, new BreakpointLogEntry(id,
                        BreakpointOptions.FormatLogMessage(updated.LogMessage, _ => "41.80"), updated.HitCount));
                    continue;
                }
                if (!HitCountGate(updated) || !ConditionGate(updated))
                    continue;
                var rendered = Render(id, session);
                session.Mode = "break";
                session.LastHit = $"{rendered.Id} {rendered.File}:{rendered.Line}";
                return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: $"hit {rendered.Id}");
            }
        }
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: "running; no bound breakpoints armed");
    }

    public async Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (session.Mode != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {session.Mode}", "resume to a breakpoint or pause first; only inspect and step in break mode");
        if (ForceTimeout)
            return new ExecutionResult(session.Mode, session.LastHit, Status: "timeout", Note: "step exceeded timeoutMs; mode unchanged, retry or pause");
        return new ExecutionResult(session.Mode, LastHit: null, Status: "ok", Note: $"stepped {direction}");
    }

    private BreakpointInfo Render(string id, FakeSession session)
    {
        var bp = session.Breakpoints[id];
        return SymbolsLoaded
            ? new BreakpointInfo(id, bp.File, bp.Line, Bound: true, Warning: null,
                bp.Condition, bp.HitCountTarget, bp.HitCountMode, bp.HitCount, bp.LogMessage)
            : new BreakpointInfo(id, bp.File, bp.Line, Bound: false, Warning: "symbols not loaded yet; poll listsession.Breakpoints until bound",
                bp.Condition, bp.HitCountTarget, bp.HitCountMode, bp.HitCount, bp.LogMessage);
    }

    private static int BpIndex(string id) => int.TryParse(id.AsSpan(2), out var n) ? n : int.MaxValue;

    private static bool HitCountGate(FakeBreakpoint bp) =>
        bp.HitCountTarget is null || (bp.HitCountMode == HitCountModes.Every
            ? bp.HitCount % bp.HitCountTarget.Value == 0
            : bp.HitCount == bp.HitCountTarget.Value);

    private static bool ConditionGate(FakeBreakpoint bp)
    {
        if (bp.Condition is null)
            return true;
        var cond = bp.Condition.Trim();
        // Deterministic fake truthiness: only an explicit false/0 fails; the live
        // bridge evaluates the real expression per hit instead.
        return !cond.Equals("false", StringComparison.OrdinalIgnoreCase) && cond != "0";
    }

    private static void RequireBreak(FakeSession session)
    {
        if (session.Mode != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {session.Mode}",
                "resume to a breakpoint or pause first; only inspect and step in break mode");
    }

    private static IReadOnlyList<StackFrameInfo> CannedFrames() =>
    [
        new StackFrameInfo("f0", "Order.Total()", "Shop.dll", "Order.cs", 17),
        new StackFrameInfo("f1", "Program.Main()", "Shop.dll", "Program.cs", 42),
    ];

    private static IReadOnlyList<ValueInfo> CannedTopLevel() =>
    [
        new ValueInfo("items", "List<Item>", "Count = 1,247 [truncated]", Expandable: true, Path: "items"),
        new ValueInfo("total", "decimal", "41.80", Expandable: false, Path: null),
    ];

    private static IReadOnlyList<ValueInfo> CannedChildren() =>
    [
        new ValueInfo("items.Count", "int", "1247", Expandable: false, Path: null),
        new ValueInfo("items[0]", "Item", "{Id=1, ...} [truncated]", Expandable: true, Path: "items[0]"),
    ];

    public async Task<ReadStackResult> ReadStackAsync(string sessionId, int maxFrames, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        RequireBreak(session);
        return new ReadStackResult(CannedFrames().Take(maxFrames).ToList(), session.Mode, session.LastHit);
    }

    public async Task<ReadVariablesResult> ReadVariablesAsync(
        string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        RequireBreak(session);
        if (CannedFrames().All(f => f.Id != frameId))
            throw new DebugDomainException(
                "frame-not-found", $"no frame {frameId}", "call read_stack for valid frame ids");
        if (path is null)
            return new ReadVariablesResult(frameId, Path: null, CannedTopLevel().Take(maxVariables).ToList(), session.Mode, session.LastHit);
        if (path != "items")
            throw new DebugDomainException(
                "path-not-found", $"no expandable path {path} on frame {frameId}",
                "drill down only via expandable paths returned by read_variables");
        return new ReadVariablesResult(frameId, path, CannedChildren().Take(maxVariables).ToList(), session.Mode, session.LastHit);
    }

    public async Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        RequireBreak(session);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (ForceTimeout)
            throw new DebugDomainException(
                "timeout", "evaluate exceeded timeoutMs", "retry with a larger timeoutMs or a simpler expression");
        if (ForceInvalidExpression)
            throw new DebugDomainException(
                "invalid-expression", $"expression parsed but did not evaluate: {expression}",
                "pass a read-only expression valid in the current frame");
        // Read-only by construction: the fake holds no live handles and this path never mutates state.
        return new EvaluateResult(expression, Value: "41.80", Type: "decimal", Expandable: false, session.Mode, session.LastHit);
    }

    public async Task<ExecutionResult> AttachProcessAsync(
        string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (!consent)
            throw new DebugDomainException(
                "consent-required", $"attaching to process {processId} needs explicit consent",
                "re-call attach_process with consent: true");
        if (!ProcessOwners.TryGetValue(processId, out var owner))
            throw new DebugDomainException(
                "process-not-found", $"no local process {processId}", "list local processes and retry");
        if (!owner.Equals(CurrentUser, StringComparison.OrdinalIgnoreCase))
            throw new DebugDomainException(
                "user-mismatch", $"process {processId} belongs to {owner}, bridge runs as {CurrentUser}",
                "only attach to your own processes");
        session.TargetPid = processId;
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: $"attached to process {processId}");
    }

    public async Task<LaunchProcessResult> LaunchProcessAsync(string sessionId, string executablePath, string? arguments,
        string? workingDirectory, IReadOnlyDictionary<string, string>? environment, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        // Same order as the live bridge: start rules first, then the binding check.
        _ = ProcessLaunch.CheckStart(executablePath, workingDirectory);
        if (session.TargetPid is { } bound)
            throw ProcessLaunch.AlreadyBound(bound);
        var pid = ++_pidCounter;
        session.TargetPid = pid;
        // Attached from birth: the phantom run starts like a resume from design mode.
        var run = await ResumeAsync(sessionId, timeoutMs, ct);
        return new LaunchProcessResult(pid, run.Mode, run.LastHit, run.Status, $"launched target process {pid}; {run.Note}");
    }
}
