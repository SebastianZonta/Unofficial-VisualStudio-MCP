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
        /// <summary>Breakpoints this session created, keyed by stable id.</summary>
        public Dictionary<string, FakeBreakpoint> Breakpoints { get; } = new();
        /// <summary>Per-session id counter: only goes up, so ids are never reused.</summary>
        public int BreakpointCounter { get; set; }
        /// <summary>Rest of the VS instance's breakpoint list: set by the user or another session.</summary>
        public List<(string File, int Line)> ForeignBreakpoints { get; } = new();
        public List<BreakpointLogEntry> Log { get; } = new();
        public bool Dead { get; set; }
        /// <summary>Target PID bound via attach_process or launch_process; null while unbound.</summary>
        public int? TargetPid { get; set; }
        /// <summary>Seeded module table of the session's target process.</summary>
        public List<ModuleInfo> Modules { get; } = new();
        /// <summary>Exception-break settings, copied from the catalog at attach: per session, never shared.</summary>
        public Dictionary<string, string> ExceptionModes { get; } = new(StringComparer.Ordinal);
        /// <summary>Break-when-thrown values this session found at attach, so default restores them.</summary>
        public Dictionary<string, string> FoundExceptionModes { get; } = new(StringComparer.Ordinal);
        /// <summary>Throw armed for the next run; a null Message simulates a read refused under nse.</summary>
        public FakeThrow? PendingThrow { get; set; }
        /// <summary>Exception the session is stopped on; null on every other stop.</summary>
        public FakeThrow? CurrentException { get; set; }
        /// <summary>Values written via set_variable, keyed by (frameId, variable name): per session, never shared.</summary>
        public Dictionary<(string FrameId, string Name), (string Value, string Type)> Writes { get; } = new();
    }

    private sealed record FakeThrow(string Type, string? Message, bool Handled);

    private sealed record FakeBreakpoint(
        string File, int Line, string? Condition, int? HitCountTarget, string? HitCountMode,
        int HitCount, string? LogMessage)
    {
        public FakeBreakpoint WithHitCount(int hitCount) =>
            this with { HitCount = hitCount };
    }

    private readonly Dictionary<string, FakeSession> _sessions = new();
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

    /// <summary>Fake for a busy VS: the next N remove_breakpoint calls fail with vs-busy and delete nothing.</summary>
    public int RemoveBusyFailures { get; set; }

    /// <summary>Fake for a parsed-but-not-evaluable expression. When true, evaluate returns invalid-expression.</summary>
    public bool ForceInvalidExpression { get; set; }

    public List<VsInstanceInfo> Instances { get; } = new();

    /// <summary>Exception types every new session starts with, and their default break mode.</summary>
    public Dictionary<string, string> ExceptionCatalog { get; } = new(StringComparer.Ordinal)
    {
        ["System.ArgumentException"] = ExceptionBreakModes.UnhandledOnly,
        ["System.InvalidOperationException"] = ExceptionBreakModes.UnhandledOnly,
        ["System.NullReferenceException"] = ExceptionBreakModes.UnhandledOnly,
    };

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
        var session = new FakeSession { InstanceKey = key };
        foreach (var (name, mode) in ExceptionCatalog)
        {
            session.ExceptionModes[name] = mode;
            session.FoundExceptionModes[name] = mode;
        }
        _sessions[sessionId] = session;
        return Task.FromResult(new AttachResult(sessionId, "design", LastHit: null));
    }

    public Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Fake marker: canned values must never be mistaken for a real VS instance.
        return Task.FromResult(new ListInstancesResult(Instances.Select(i => i with { DisplayName = $"{i.DisplayName} [fake]" }).ToList()));
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

    private static DebugDomainException NoSession() =>
        new("no-session", "no active session", "call attach first");

    private static DebugDomainException VsGone(string sessionId) =>
        new("vs-not-running", $"the VS instance bound to session {sessionId} is gone", "call attach again");

    private Task<FakeSession> RequireAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw NoSession();
        if (session.Dead)
            throw VsGone(sessionId);
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
        // Inventory lists only live sessions: dead records stay until detach for rebind,
        // but never surface as actionable handles.
        return Task.FromResult(new ListSessionsResult(
            _sessions.Where(kv => !kv.Value.Dead).Select(kv => new SessionInfo(kv.Key, $"{kv.Value.InstanceKey} [fake]")).ToList()));
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
        BreakpointOptions.ValidateLocation(file, line);
        var error = BreakpointOptions.Check(condition, hitCount, hitCountMode, logMessage);
        if (error is not null)
            throw new DebugDomainException(ErrorCodes.InvalidArgument, error.Value.Detail, error.Value.Remediation);
        var (cond, count, mode, log) = BreakpointOptions.Normalize(condition, hitCount, hitCountMode, logMessage);
        // Occupied location, like the live bridge: own repeat returns its id, foreign is refused.
        foreach (var (ownedId, bp) in session.Breakpoints)
        {
            if (!BreakpointOptions.SameLocation(bp.File, bp.Line, file, line))
                continue;
            var existing = Render(ownedId, session);
            BreakpointOptions.RequireSameOptions(existing, cond, count, mode, log);
            return new SetBreakpointResult(existing, session.Mode, session.LastHit, Created: false);
        }
        var foreign = session.ForeignBreakpoints.FindIndex(bp => BreakpointOptions.SameLocation(bp.File, bp.Line, file, line));
        if (foreign >= 0)
            throw BreakpointOptions.Exists(BreakpointOptions.ForeignId(foreign + 1), file, line);
        var id = BreakpointOptions.OwnedId(++session.BreakpointCounter);
        session.Breakpoints[id] = new FakeBreakpoint(file, line, cond, count, mode, HitCount: 0, log);
        return new SetBreakpointResult(Render(id, session), session.Mode, session.LastHit);
    }

    public async Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        var items = session.Breakpoints.Keys.Select(bp => Render(bp, session)).ToList();
        items.AddRange(session.ForeignBreakpoints.Select((bp, i) => new BreakpointInfo(
            BreakpointOptions.ForeignId(i + 1), bp.File, bp.Line, Bound: true, Warning: BreakpointOptions.ForeignWarning)));
        return new ListBreakpointsResult(items, session.Mode, session.LastHit, session.Log.ToList());
    }

    public async Task<ListModulesResult> ListModulesAsync(string sessionId, string? nameFilter, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        ModuleListing.RequireDebugging(session.Mode);
        return new ListModulesResult(ModuleListing.FilterAndOrder(session.Modules, nameFilter), session.Mode, session.LastHit);
    }

    /// <summary>Test hook: seeds the module table of the session's target process.</summary>
    public void SetModules(string sessionId, IEnumerable<ModuleInfo> modules)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Modules.Clear();
            session.Modules.AddRange(modules);
        }
    }

    public async Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        if (RemoveBusyFailures > 0)
        {
            // Busy rejection: nothing deleted, the id stays owned (like the live bridge).
            RemoveBusyFailures--;
            throw new DebugDomainException("vs-busy", "Visual Studio rejected the automation call", "dismiss any modal dialog in VS and retry");
        }
        if (!session.Breakpoints.Remove(breakpointId))
            throw BreakpointOptions.NotFound(breakpointId);
        return new RemoveBreakpointResult(breakpointId, session.Mode, session.LastHit);
    }

    /// <summary>Test hook: a breakpoint in the session's VS instance that this session did not
    /// create (set by hand in VS, or by another debug session).</summary>
    public void AddForeignBreakpoint(string sessionId, string file, int line)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.ForeignBreakpoints.Add((file, line));
    }

    public async Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        RequireRunMode(session.Mode);
        session.Mode = "break";
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: "paused on request");
    }

    public async Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        RequireNotRunMode(session.Mode);
        if (ForceTimeout)
            return new ExecutionResult(session.Mode, session.LastHit, Status: "timeout", Note: "resume exceeded timeoutMs; still waiting");
        return RunUntilStop(session);
    }

    public Task<ExecutionResult> RunToCursorAsync(string sessionId, string file, int line, int timeoutMs, CancellationToken ct) =>
        RunToCursor.RunAsync(this, sessionId, file, line, async mode =>
        {
            if (mode == "break")
                return await ResumeAsync(sessionId, timeoutMs, ct);
            // Already running: only wait for the next stop.
            var session = await RequireAsync(sessionId, ct);
            return ForceTimeout
                ? new ExecutionResult(session.Mode, session.LastHit, Status: "timeout", Note: "resume exceeded timeoutMs; still waiting")
                : RunUntilStop(session);
        }, ct);

    private ExecutionResult RunUntilStop(FakeSession session)
    {
        session.Mode = "run";
        session.LastHit = null;
        session.CurrentException = null;
        // An armed throw fires first: it breaks when its type is first-chance or nothing handles it.
        if (TryBreakOnThrow(session) is { } throwHit)
            return throwHit;
        if (ScanBreakpoints(session) is { } bpHit)
            return bpHit;
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: "running; no bound breakpoints armed");
    }

    private static ExecutionResult? TryBreakOnThrow(FakeSession session)
    {
        if (session.PendingThrow is not { } thrown)
            return null;
        session.PendingThrow = null;
        var firstChance = session.ExceptionModes.GetValueOrDefault(thrown.Type) == ExceptionBreakModes.FirstChance;
        if (!firstChance && thrown.Handled)
            return null;
        session.Mode = "break";
        session.CurrentException = thrown;
        var info = new ExceptionBreakInfo(thrown.Type, ExceptionConfig.ExceptionPath, ExceptionConfig.Truncate(thrown.Message));
        return new ExecutionResult(session.Mode, LastHit: null, Status: "ok",
            ExceptionConfig.BreakNote(firstChance ? "first-chance" : "unhandled", info), info);
    }

    private ExecutionResult? ScanBreakpoints(FakeSession session)
    {
        // Evaluate-then-decide per hit, in creation order: the counter advances on
        // every evaluation pass; a logpoint appends and never stops; a stopping
        // breakpoint fires only when its hit-count mode and condition both agree.
        if (!SymbolsLoaded)
            return null;
        foreach (var id in session.Breakpoints.Keys.OrderBy(BpIndex).ToList())
        {
            if (EvalBreakpoint(session, id) is { } hit)
                return hit;
        }
        return null;
    }

    private ExecutionResult? EvalBreakpoint(FakeSession session, string id)
    {
        var bp = session.Breakpoints[id];
        session.Breakpoints[id] = bp.WithHitCount(bp.HitCount + 1);
        var updated = session.Breakpoints[id];
        if (updated.LogMessage is not null)
        {
            BreakpointOptions.AppendLog(session.Log, new BreakpointLogEntry(id,
                BreakpointOptions.FormatLogMessage(updated.LogMessage, _ => "41.80"), updated.HitCount));
            return null;
        }
        if (!HitCountGate(updated) || !ConditionGate(updated))
            return null;
        var rendered = Render(id, session);
        session.Mode = "break";
        session.LastHit = $"{rendered.File}:{rendered.Line}";
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: $"hit {rendered.Id}");
    }

    public async Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        RequireBreakMode(session.Mode);
        RequireStepDirection(direction);
        // One shape on fast and over-budget paths: the post-step snapshot.
        if (ForceTimeout)
            return new ExecutionResult(session.Mode, session.LastHit, Status: "timeout", Note: "step exceeded timeoutMs; mode unchanged, retry or pause");
        return new ExecutionResult(session.Mode, session.LastHit, Status: "ok", Note: $"stepped {direction}");
    }

    private BreakpointInfo Render(string id, FakeSession session)
    {
        var bp = session.Breakpoints[id];
        return SymbolsLoaded
            ? new BreakpointInfo(id, bp.File, bp.Line, Bound: true, Warning: null,
                bp.Condition, bp.HitCountTarget, bp.HitCountMode, bp.HitCount, bp.LogMessage)
            : new BreakpointInfo(id, bp.File, bp.Line, Bound: false, Warning: "symbols not loaded yet; poll list_breakpoints until bound",
                bp.Condition, bp.HitCountTarget, bp.HitCountMode, bp.HitCount, bp.LogMessage);
    }

    private static int BpIndex(string id) => int.TryParse(id.AsSpan(2), out var n) ? n : int.MaxValue;

    private static bool HitCountGate(FakeBreakpoint bp) =>
        bp.HitCountTarget is null || (bp.HitCountMode == HitCountModes.Every
            ? bp.HitCount % bp.HitCountTarget.Value == 0
            : bp.HitCount == bp.HitCountTarget.Value);

    private static bool ConditionGate(FakeBreakpoint bp) =>
        // Deterministic fake truthiness: only an explicit false/0 fails; the live
        // bridge evaluates the real expression per hit instead.
        bp.Condition is null || (bp.Condition.Trim() is var cond
            && !cond.Equals("false", StringComparison.OrdinalIgnoreCase) && cond != "0");

    // Mode guards, private to this bridge (shared-rules files are owned elsewhere);
    // DteDebugBridge carries the same four for its debugger modes.
    private static void RequireRunMode(string mode)
    {
        if (mode != "run")
            throw new DebugDomainException(
                "not-running", $"debuggee is {mode}, nothing to pause", "resume first, then pause while running");
    }

    private static void RequireNotRunMode(string mode)
    {
        if (mode == "run")
            throw new DebugDomainException(
                "already-running", "debuggee is already running", "pause first to inspect");
    }

    private static void RequireBreakMode(string mode)
    {
        if (mode != "break")
            throw new DebugDomainException(
                "not-in-break-mode", $"debuggee is {mode}",
                "resume to a breakpoint or pause first; only inspect and step in break mode");
    }

    private static void RequireStepDirection(string direction)
    {
        if (direction is not ("over" or "into" or "out"))
            throw new DebugDomainException(ErrorCodes.InvalidArgument, $"unknown step direction {direction}", "pass over, into, or out");
    }

    private static void RequireBreak(FakeSession session) => RequireBreakMode(session.Mode);

    private static IReadOnlyList<StackFrameInfo> CannedFrames() =>
    [
        // Live EnvDTE exposes no file/line on frames: fake matches live with nulls.
        new StackFrameInfo("f0", "Order.Total()", "Shop.dll", null, null),
        new StackFrameInfo("f1", "Program.Main()", "Shop.dll", null, null),
    ];

    // Per-frame canned variables: the same names carry different values in f0 and f1,
    // so frame-true reads (and evaluate pinned to f0) are observable at the tool seam.
    private static IReadOnlyList<ValueInfo> CannedTopLevel(string frameId) => frameId == "f0"
        ?
        [
            new ValueInfo("items", "List<Item>", "Count = 1,247 [truncated]", Expandable: true, Path: "items"),
            new ValueInfo("order", "Order", "{Id=1001, ...} [truncated]", Expandable: true, Path: "order"),
            new ValueInfo("total", "decimal", "41.80", Expandable: false, Path: null),
        ]
        :
        [
            new ValueInfo("items", "List<Item>", "Count = 3", Expandable: true, Path: "items"),
            new ValueInfo("total", "decimal", "99.00", Expandable: false, Path: null),
        ];

    private static IReadOnlyList<ValueInfo> CannedOrderChildren() =>
    [
        new ValueInfo("order.Id", "int", "1001", Expandable: false, Path: null),
        new ValueInfo("order.Status", "string", "Paid", Expandable: false, Path: null),
        new ValueInfo("order.Total", "decimal", "5625.00", Expandable: false, Path: null),
    ];

    private static IReadOnlyList<ValueInfo> CannedChildren(string frameId) => frameId == "f0"
        ?
        [
            new ValueInfo("items.Count", "int", "1247", Expandable: false, Path: null),
            new ValueInfo("items[0]", "Item", "{Id=1, ...} [truncated]", Expandable: true, Path: "items[0]"),
        ]
        :
        [
            new ValueInfo("items.Count", "int", "3", Expandable: false, Path: null),
            new ValueInfo("items[0]", "Item", "{Id=7, ...}", Expandable: true, Path: "items[0]"),
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
            return new ReadVariablesResult(frameId, Path: null, Written(session, frameId, CannedTopLevel(frameId)).Take(maxVariables).ToList(), session.Mode, session.LastHit);
        if (path == ExceptionConfig.ExceptionPath && session.CurrentException is { } current)
            return new ReadVariablesResult(frameId, path,
            [
                new ValueInfo("Message", "string", ExceptionConfig.Truncate(current.Message) ?? "(unavailable)", Expandable: false, Path: null),
            ], session.Mode, session.LastHit);
        if (path == "items[0]")
            return new ReadVariablesResult(frameId, path,
            [
                new ValueInfo("items[0].Id", "int", "1", Expandable: false, Path: null),
            ], session.Mode, session.LastHit);
        if (path != "items" && path != "order")
            throw new DebugDomainException(
                "path-not-found", $"no expandable path {path} on frame {frameId}",
                "drill down only via expandable paths returned by read_variables");
        return new ReadVariablesResult(frameId, path,
            Written(session, frameId, path == "order" ? CannedOrderChildren() : CannedChildren(frameId)).Take(maxVariables).ToList(), session.Mode, session.LastHit);
    }

    private static IEnumerable<ValueInfo> Written(FakeSession session, string frameId, IEnumerable<ValueInfo> canned) =>
        canned.Select(v => session.Writes.TryGetValue((frameId, v.Name), out var w) ? v with { Value = w.Value, Type = w.Type } : v);

    // Get-only in the fake's model (List<T>.Count), so writes to it are read-only targets.
    private static readonly HashSet<string> ReadOnlyNames = new(StringComparer.Ordinal) { "items.Count" };

    public async Task<SetVariableResult> SetVariableAsync(
        string sessionId, string frameId, string path, string value, bool consent, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        // Same order as the live bridge: consent before any debugger state is read.
        VariableWrite.RequireConsent(consent, frameId, path);
        RequireBreak(session);
        if (CannedFrames().All(f => f.Id != frameId))
            throw new DebugDomainException(
                "frame-not-found", $"no frame {frameId}", "call read_stack for valid frame ids");
        var target = Written(session, frameId, CannedTopLevel(frameId).Concat(CannedChildren(frameId)))
            .FirstOrDefault(v => v.Name == path.Trim())
            ?? throw VariableWrite.PathNotFound(frameId, path);
        if (ReadOnlyNames.Contains(target.Name))
            throw VariableWrite.ReadOnly(frameId, path, "get-only property");
        // Echo what VS would re-read: the coerced value as displayed, not the agent's spelling.
        var shown = VariableWrite.Shown(VariableWrite.Coerce(target.Type, value));
        session.Writes[(frameId, target.Name)] = (shown, target.Type);
        return new SetVariableResult(frameId, path, shown, target.Type, session.Mode, session.LastHit);
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
                "invalid-expression", $"expression did not evaluate without side effects: {expression}",
                "pass a read-only expression valid in the top frame; prefer fields and property reads over method calls");
        if (expression.Trim() == $"{ExceptionConfig.ExceptionPath}.Message" && session.CurrentException is { } currentEx)
            return new EvaluateResult(expression, ExceptionConfig.Truncate(currentEx.Message) ?? "(unavailable)", "string", Expandable: false, session.Mode, session.LastHit);
        // Indexer drill paths round-trip without edit: items[0] evaluates like live.
        if (expression.Trim() == "items[0]")
            return new EvaluateResult(expression, "{Id=1, ...}", "Item", Expandable: true, session.Mode, session.LastHit);
        // Read-only by construction: the fake holds no live handles and this path never mutates state.
        // Evaluate always runs in the top frame (f0), whatever frame was inspected before.
        // Unknown names fail like live (invalid-expression), never a canned value.
        var top = CannedTopLevel("f0").FirstOrDefault(v => v.Name == expression.Trim());
        if (top is not null)
            return new EvaluateResult(expression, top.Value, top.Type, top.Expandable, session.Mode, session.LastHit);
        throw new DebugDomainException(
            "invalid-expression", $"expression did not evaluate without side effects: {expression}",
            "pass a read-only expression valid in the top frame; prefer fields and property reads over method calls");
    }

    public async Task<ExecutionResult> AttachProcessAsync(
        string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct,
        string? expectedProcessName = null)
    {
        var session = await RequireAsync(sessionId, ct);
        _ = timeoutMs;
        ct.ThrowIfCancellationRequested();
        if (!consent)
            throw new DebugDomainException(
                "consent-required", $"attaching to process {processId} needs explicit consent",
                "re-call attach_process with consent: true");
        // A VS PID (devenv) is never an attach target: listing still shows VS
        // instances for diagnosis, but attaching would bind the IDE to itself.
        var known = Processes.FirstOrDefault(p => p.Pid == processId);
        if (known is not null)
            ProcessLaunch.CheckExpectedName(known.Name, expectedProcessName, processId);
        if (known is not null && Path.GetFileNameWithoutExtension(known.Name).Equals("devenv", StringComparison.OrdinalIgnoreCase))
            throw new DebugDomainException(
                ErrorCodes.InvalidArgument, $"devenv (PID {processId}) is a VS instance, never a target process",
                "pass the program under test, not Visual Studio itself");
        if (!ProcessOwners.TryGetValue(processId, out var owner))
            throw new DebugDomainException(
                "process-not-found", $"no local process {processId}", "list local processes and retry");
        if (!owner.Equals(CurrentUser, StringComparison.OrdinalIgnoreCase))
            throw new DebugDomainException(
                "user-mismatch", $"process {processId} belongs to {owner}, bridge runs as {CurrentUser}",
                "only attach to your own processes");
        if (session.TargetPid is { } bound)
            throw ProcessLaunch.AlreadyBound(bound);
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
        return new LaunchProcessResult(pid, run.Mode, run.LastHit, run.Status, $"launched target process {pid}; {run.Note}", run.Exception);
    }

    /// <summary>Test hook: the next run throws <paramref name="type"/>. Handled throws break only when
    /// the type is first-chance; a null message simulates a message read refused under nse.</summary>
    public void ThrowOnNextRun(string sessionId, string type, string? message, bool handled = true)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.PendingThrow = new FakeThrow(type, message, handled);
    }

    public async Task<ListExceptionSettingsResult> ListExceptionSettingsAsync(string sessionId, string? nameFilter, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        return new ListExceptionSettingsResult(ExceptionConfig.FilterAndOrder(
            session.ExceptionModes.Select(kv => new ExceptionSettingInfo(kv.Key, kv.Value)), nameFilter),
            session.Mode, session.LastHit);
    }

    public async Task<SetExceptionSettingResult> SetExceptionSettingAsync(string sessionId, string exceptionType, string mode, CancellationToken ct)
    {
        var session = await RequireAsync(sessionId, ct);
        ExceptionConfig.RequireNotRunning(session.Mode);
        ExceptionConfig.ValidateMode(mode);
        if (!session.ExceptionModes.ContainsKey(exceptionType))
            throw ExceptionConfig.NotFound(exceptionType);
        session.ExceptionModes[exceptionType] = mode == ExceptionBreakModes.Default
            ? session.FoundExceptionModes.GetValueOrDefault(exceptionType, ExceptionCatalog.GetValueOrDefault(exceptionType, ExceptionBreakModes.UnhandledOnly))
            : mode;
        return new SetExceptionSettingResult(
            new ExceptionSettingInfo(exceptionType, session.ExceptionModes[exceptionType]), session.Mode, session.LastHit);
    }
}
