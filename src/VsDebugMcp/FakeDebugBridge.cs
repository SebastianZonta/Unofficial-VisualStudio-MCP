namespace VsDebugMcp;

/// <summary>In-memory fake session backing the tickets. Single active session: a new attach replaces the old binding.</summary>
public sealed class FakeDebugBridge : IDebugBridge
{
    private string? _activeSessionId;
    private readonly Dictionary<string, (string File, int Line)> _breakpoints = new();
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

    private string _mode = "design";
    private string? _lastHit;

    public Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken ct)
    {
        _ = instanceOverride;
        ct.ThrowIfCancellationRequested();
        _activeSessionId = Guid.NewGuid().ToString("N");
        _mode = "design";
        _lastHit = null;
        return Task.FromResult(new AttachResult(_activeSessionId, _mode, _lastHit));
    }

    public Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ListInstancesResult(Instances.ToList()));
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
        return Task.CompletedTask;
    }

    public async Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        var id = $"bp{++_breakpointCounter}";
        _breakpoints[id] = (file, line);
        return new SetBreakpointResult(Render(id), _mode, _lastHit);
    }

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        var items = _breakpoints.Keys.Select(Render).ToList();
        return new ListBreakpointsResult(items, _mode, _lastHit);
    }

    public async Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        if (!_breakpoints.Remove(breakpointId))
            throw new DebugDomainException(
                "breakpoint-not-found", $"no breakpoint {breakpointId}", "call list_breakpoints for valid ids");
        return new RemoveBreakpointResult(breakpointId, _mode, _lastHit);
    }

    public async Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        if (_mode != "run")
            throw new DebugDomainException(
                "not-running", $"debuggee is {_mode}, nothing to pause", "resume first, then pause while running");
        _mode = "break";
        return new ExecutionResult(_mode, _lastHit, Status: "ok", Note: "paused on request");
    }

    public async Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (_mode == "run")
            throw new DebugDomainException(
                "already-running", "debuggee is already running", "pause first to inspect");
        if (ForceTimeout)
            return new ExecutionResult(_mode, _lastHit, Status: "timeout", Note: "resume exceeded timeoutMs; still waiting");
        _mode = "run";
        _lastHit = null;
        var hit = _breakpoints.Keys.Select(Render).FirstOrDefault(b => b.Bound);
        if (hit is not null)
        {
            _mode = "break";
            _lastHit = $"{hit.Id} {hit.File}:{hit.Line}";
            return new ExecutionResult(_mode, _lastHit, Status: "ok", Note: $"hit {hit.Id}");
        }
        return new ExecutionResult(_mode, _lastHit, Status: "ok", Note: "running; no bound breakpoints armed");
    }

    public async Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (_mode != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {_mode}", "resume to a breakpoint or pause first; only inspect and step in break mode");
        if (ForceTimeout)
            return new ExecutionResult(_mode, _lastHit, Status: "timeout", Note: "step exceeded timeoutMs; mode unchanged, retry or pause");
        return new ExecutionResult(_mode, LastHit: null, Status: "ok", Note: $"stepped {direction}");
    }

    private BreakpointInfo Render(string id)
    {
        var (file, line) = _breakpoints[id];
        return SymbolsLoaded
            ? new BreakpointInfo(id, file, line, Bound: true, Warning: null)
            : new BreakpointInfo(id, file, line, Bound: false, Warning: "symbols not loaded yet; poll list_breakpoints until bound");
    }

    private void RequireBreak()
    {
        if (_mode != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {_mode}",
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
        await RequireSessionAsync(sessionId, ct);
        RequireBreak();
        return new ReadStackResult(CannedFrames().Take(maxFrames).ToList(), _mode, _lastHit);
    }

    public async Task<ReadVariablesResult> ReadVariablesAsync(
        string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        RequireBreak();
        if (CannedFrames().All(f => f.Id != frameId))
            throw new DebugDomainException(
                "frame-not-found", $"no frame {frameId}", "call read_stack for valid frame ids");
        if (path is null)
            return new ReadVariablesResult(frameId, Path: null, CannedTopLevel().Take(maxVariables).ToList(), _mode, _lastHit);
        if (path != "items")
            throw new DebugDomainException(
                "path-not-found", $"no expandable path {path} on frame {frameId}",
                "drill down only via expandable paths returned by read_variables");
        return new ReadVariablesResult(frameId, path, CannedChildren().Take(maxVariables).ToList(), _mode, _lastHit);
    }

    public async Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
        RequireBreak();
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
        return new EvaluateResult(expression, Value: "41.80", Type: "decimal", Expandable: false, _mode, _lastHit);
    }

    public async Task<ExecutionResult> AttachProcessAsync(
        string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct)
    {
        await RequireSessionAsync(sessionId, ct);
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
        return new ExecutionResult(_mode, _lastHit, Status: "ok", Note: $"attached to process {processId}");
    }
}
