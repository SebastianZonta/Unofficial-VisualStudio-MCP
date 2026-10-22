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
        public Dictionary<string, (string File, int Line)> Breakpoints { get; } = new();
        public bool Dead { get; set; }
    }

    private readonly Dictionary<string, FakeSession> _sessions = new();
    private int _breakpointCounter;

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

    public async Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        var id = $"bp{++_breakpointCounter}";
        session.Breakpoints[id] = (file, line);
        return new SetBreakpointResult(Render(id, session), session.Mode, session.LastHit);
    }

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        var items = session.Breakpoints.Keys.Select(bp => Render(bp, session)).ToList();
        return new ListBreakpointsResult(items, session.Mode, session.LastHit);
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
        var hit = session.Breakpoints.Keys.Select(bp => Render(bp, session)).FirstOrDefault(b => b.Bound);
        if (hit is not null)
        {
            session.Mode = "break";
            session.LastHit = $"{hit.Id} {hit.File}:{hit.Line}";
            return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: $"hit {hit.Id}");
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
        var (file, line) = session.Breakpoints[id];
        return SymbolsLoaded
            ? new BreakpointInfo(id, file, line, Bound: true, Warning: null)
            : new BreakpointInfo(id, file, line, Bound: false, Warning: "symbols not loaded yet; poll listsession.Breakpoints until bound");
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
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: $"attached to process {processId}");
    }
}
