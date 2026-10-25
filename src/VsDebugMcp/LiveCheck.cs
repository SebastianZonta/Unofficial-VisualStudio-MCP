namespace VsDebugMcp;

/// <summary>Builder-only harness for the ticket-11 manual check: attach + set + list
/// against the real VS 2026, print results, exit. Not an MCP tool.</summary>
internal static class LiveCheck
{
    public static async Task<int> RunFullAsync(string[] args)
    {
        var rest = Rest(args, "--live-check-full");
        var file = rest.FirstOrDefault();
        var lineText = rest.Skip(1).FirstOrDefault();
        if (file is null || !int.TryParse(lineText, out var line))
            return Usage("usage: VsDebugMcp --live-check-full <file> <line>");
        using var bridge = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var attach = await bridge.AttachAsync(null, cts.Token);
            Console.WriteLine($"attach ok: session={attach.SessionId} mode={attach.Mode}");
            var set = await bridge.SetBreakpointAsync(attach.SessionId, file, line, cts.Token);
            Console.WriteLine($"set ok: id={set.Breakpoint.Id} bound={set.Breakpoint.Bound}");
            var resumed = await bridge.ResumeAsync(attach.SessionId, 120_000, cts.Token);
            Console.WriteLine($"resume: status={resumed.Status} mode={resumed.Mode} lastHit={resumed.LastHit}");
            if (resumed.Mode != "break")
            {
                Console.Error.WriteLine("did not land in break mode; cannot inspect");
                return 1;
            }
            var stack = await bridge.ReadStackAsync(attach.SessionId, 20, cts.Token);
            Console.WriteLine($"stack ok: frames={stack.Frames.Count}");
            foreach (var f in stack.Frames)
                Console.WriteLine($"  {f.Id} {f.Function} [{f.Module}]");
            var modules = await bridge.ListModulesAsync(attach.SessionId, null, cts.Token);
            Console.WriteLine($"modules ok: count={modules.Modules.Count} mode={modules.Mode}");
            foreach (var m in modules.Modules.Take(10))
                Console.WriteLine($"  {m.Name} loaded={m.Loaded} symbols={m.SymbolState} {m.SymbolDetail}");
            var vars = await bridge.ReadVariablesAsync(attach.SessionId, stack.Frames[0].Id, null, 50, cts.Token);
            Console.WriteLine($"variables ok: frame={vars.FrameId} count={vars.Variables.Count}");
            foreach (var v in vars.Variables)
                Console.WriteLine($"  {v.Name} : {v.Type} = {v.Value} expandable={v.Expandable}");
            var eval = await bridge.EvaluateAsync(attach.SessionId, "args.Length", 30_000, cts.Token);
            Console.WriteLine($"evaluate ok: {eval.Expression} = {eval.Value} ({eval.Type})");
            var stepped = await bridge.StepAsync(attach.SessionId, "over", 60_000, cts.Token);
            Console.WriteLine($"step ok: status={stepped.Status} mode={stepped.Mode}");
            await bridge.RemoveBreakpointAsync(attach.SessionId, set.Breakpoint.Id, cts.Token);
            Console.WriteLine("remove ok: core loop complete");
            return 0;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
    }

    /// <summary>launch_process manual check: entry-point breakpoint binds at startup, and
    /// args (a1 a2), working directory (the temp dir) and env (LIVE_CHECK=launched) pass through.</summary>
    public static async Task<int> RunLaunchAsync(string[] args)
    {
        var rest = Rest(args, "--live-check-launch");
        if (rest.Length < 3 || !int.TryParse(rest[2], out var line))
            return Usage("usage: VsDebugMcp --live-check-launch <exe> <file> <line>");
        using var bridge = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        string? sessionId = null;
        string? bpId = null;
        try
        {
            var attach = await bridge.AttachAsync(null, cts.Token);
            sessionId = attach.SessionId;
            var set = await bridge.SetBreakpointAsync(sessionId, rest[1], line, cts.Token);
            bpId = set.Breakpoint.Id;
            var cwd = Path.GetTempPath().TrimEnd('\\');
            var launch = await bridge.LaunchProcessAsync(sessionId, rest[0], "a1 a2", cwd,
                new Dictionary<string, string> { ["LIVE_CHECK"] = "launched" }, 30_000, cts.Token);
            Console.WriteLine($"launch: pid={launch.ProcessId} status={launch.Status} mode={launch.Mode} lastHit={launch.LastHit}");
            if (launch.Mode != "break")
            {
                Console.Error.WriteLine("entry-point breakpoint did not hit at startup");
                return 1;
            }
            foreach (var expr in new[] { "args.Length", "args[1]", "System.Environment.CurrentDirectory", "System.Environment.GetEnvironmentVariable(\"LIVE_CHECK\")" })
            {
                var eval = await bridge.EvaluateAsync(sessionId, expr, 10_000, cts.Token);
                Console.WriteLine($"  {expr} = {eval.Value}");
            }
            var target = Path.GetFileNameWithoutExtension(rest[0]);
            var modules = await bridge.ListModulesAsync(sessionId, target, cts.Token);
            foreach (var m in modules.Modules)
                Console.WriteLine($"module: {m.Name} loaded={m.Loaded} symbols={m.SymbolState} {m.SymbolDetail}");
            try
            {
                await bridge.LaunchProcessAsync(sessionId, rest[0], null, null, null, 10_000, cts.Token);
                Console.Error.WriteLine("second launch while bound was not refused");
                return 1;
            }
            catch (DebugDomainException ex) when (ex.Code == ErrorCodes.AlreadyBound)
            {
                Console.WriteLine($"relaunch while bound refused: {ex.Code}");
            }
            await bridge.RemoveBreakpointAsync(sessionId, bpId, cts.Token);
            bpId = null;
            var end = await bridge.ResumeAsync(sessionId, 30_000, cts.Token);
            Console.WriteLine($"run to end: mode={end.Mode} note={end.Note}");
            return 0;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
        finally
        {
            if (sessionId is not null && bpId is not null)
                await bridge.RemoveBreakpointAsync(sessionId, bpId, CancellationToken.None);
        }
    }

    /// <summary>run_to_cursor manual check: launch, break at &lt;line&gt;, drop that breakpoint, run to
    /// &lt;toLine&gt; in the same file in one call; arrival shows the destination lastHit and the
    /// breakpoint list is unchanged. Sandbox: launch with no args (loop), TICK then AFTER_LOOP.
    /// Design-mode rejection is always checked; optional &lt;runLine&gt; (Sandbox: HEARTBEAT) adds run mode and timeout via a `wait 20` relaunch.</summary>
    public static async Task<int> RunToCursorAsync(string[] args)
    {
        var rest = Rest(args, "--live-check-run-to");
        if (rest.Length < 4 || !int.TryParse(rest[2], out var line) || !int.TryParse(rest[3], out var toLine))
            return Usage("usage: VsDebugMcp --live-check-run-to <exe> <file> <line> <toLine> [<runLine>]");
        using var bridge = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        string? sessionId = null;
        string? bpId = null;
        try
        {
            var attach = await bridge.AttachAsync(null, cts.Token);
            sessionId = attach.SessionId;
            try
            {
                await bridge.RunToCursorAsync(sessionId, rest[1], toLine, 5_000, cts.Token);
                Console.Error.WriteLine("run_to_cursor in design mode was not refused");
                return 1;
            }
            catch (DebugDomainException ex) when (ex.Code == ErrorCodes.NotRunning)
            {
                var left = (await bridge.ListBreakpointsAsync(sessionId, cts.Token)).Breakpoints.Count;
                Console.WriteLine($"design mode refused: {ex.Code}; breakpoints left={left}");
            }
            var set = await bridge.SetBreakpointAsync(sessionId, rest[1], line, cts.Token);
            bpId = set.Breakpoint.Id;
            var launch = await bridge.LaunchProcessAsync(sessionId, rest[0], null, null, null, 30_000, cts.Token);
            Console.WriteLine($"launch: pid={launch.ProcessId} mode={launch.Mode} lastHit={launch.LastHit}");
            if (launch.Mode != "break")
            {
                Console.Error.WriteLine("start breakpoint did not hit");
                return 1;
            }
            // VS can stay busy for a few seconds right after the first break (seen live as vs-busy).
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await bridge.RemoveBreakpointAsync(sessionId, bpId, cts.Token);
                    break;
                }
                catch (DebugDomainException ex) when (ex.Code == "vs-busy" && attempt < 5)
                {
                    await Task.Delay(1_000, cts.Token);
                }
            }
            bpId = null;
            // A breakpoint of mine already at the destination must survive the temporary one's removal.
            bpId = (await bridge.SetBreakpointAsync(sessionId, rest[1], toLine, cts.Token)).Breakpoint.Id;
            var before = (await bridge.ListBreakpointsAsync(sessionId, cts.Token)).Breakpoints.Count;
            var run = await bridge.RunToCursorAsync(sessionId, rest[1], toLine, 30_000, cts.Token);
            var listed = (await bridge.ListBreakpointsAsync(sessionId, cts.Token)).Breakpoints;
            var after = listed.Count;
            var survived = listed.Any(b => b.Id == bpId);
            Console.WriteLine($"run_to_cursor: mode={run.Mode} status={run.Status} lastHit={run.LastHit} breakpoints {before}->{after} mine survived={survived}");
            var arrived = run.Mode == "break" && run.LastHit?.EndsWith($":{toLine}", StringComparison.Ordinal) == true;
            if (survived)
                await bridge.RemoveBreakpointAsync(sessionId, bpId, cts.Token);
            bpId = null;
            var end = await bridge.ResumeAsync(sessionId, 30_000, cts.Token);
            Console.WriteLine($"run to end: mode={end.Mode} note={end.Note}");
            if (!(arrived && survived && before == after))
                return 1;
            if (rest.Length < 5 || !int.TryParse(rest[4], out var runLine))
                return 0;
            // Run mode and timeout: a long-running target (Sandbox `wait 20`), destination
            // <runLine> reached from run mode, then <toLine> never reached within the budget.
            await bridge.LaunchProcessAsync(sessionId, rest[0], "wait 20", null, null, 15_000, cts.Token);
            var fromRun = await bridge.RunToCursorAsync(sessionId, rest[1], runLine, 10_000, cts.Token);
            Console.WriteLine($"from run mode: mode={fromRun.Mode} lastHit={fromRun.LastHit}");
            var missed = await bridge.RunToCursorAsync(sessionId, rest[1], toLine, 2_000, cts.Token);
            var leftover = (await bridge.ListBreakpointsAsync(sessionId, cts.Token)).Breakpoints.Count;
            Console.WriteLine($"timeout: mode={missed.Mode} status={missed.Status} lastHit={missed.LastHit} breakpoints left={leftover}");
            // Still running: wait out the target via run mode (resume would be already-running).
            var drained = await bridge.RunToCursorAsync(sessionId, rest[1], toLine, 40_000, cts.Token);
            Console.WriteLine($"wait to end: mode={drained.Mode}");
            return fromRun.Mode == "break" && fromRun.LastHit?.EndsWith($":{runLine}", StringComparison.Ordinal) == true
                && missed.Mode == "run" && missed.LastHit is null && leftover == 0 ? 0 : 1;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
        finally
        {
            if (sessionId is not null && bpId is not null)
                await bridge.RemoveBreakpointAsync(sessionId, bpId, CancellationToken.None);
        }
    }

    /// <summary>Occupied-location manual check (design mode is enough): session A sets &lt;file&gt;:&lt;line&gt;;
    /// A repeating it gets the same id; session B (a second bridge, so A's breakpoint is foreign to it)
    /// is refused with breakpoint-exists, and A's breakpoint survives B's attempt.</summary>
    public static async Task<int> RunOccupiedAsync(string[] args)
    {
        var rest = Rest(args, "--live-check-occupied");
        if (rest.Length < 3 || !int.TryParse(rest[2], out var line))
            return Usage("usage: VsDebugMcp --live-check-occupied <instance> <file> <line>");
        using var bridgeA = new DteDebugBridge();
        using var bridgeB = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        string? a = null;
        try
        {
            a = (await bridgeA.AttachAsync(rest[0], cts.Token)).SessionId;
            var b = (await bridgeB.AttachAsync(rest[0], cts.Token)).SessionId;
            var first = await bridgeA.SetBreakpointAsync(a, rest[1], line, cts.Token);
            var again = await bridgeA.SetBreakpointAsync(a, rest[1], line, cts.Token);
            Console.WriteLine($"A set: {first.Breakpoint.Id}, repeat: {again.Breakpoint.Id}");
            var refused = "";
            try
            {
                var bp = await bridgeB.SetBreakpointAsync(b, rest[1], line, cts.Token);
                Console.Error.WriteLine($"B was not refused: got {bp.Breakpoint.Id}");
            }
            catch (DebugDomainException ex) when (ex.Code == ErrorCodes.BreakpointExists)
            {
                refused = ex.Message;
                Console.WriteLine($"B refused: {ex.Message}");
            }
            var listA = (await bridgeA.ListBreakpointsAsync(a, cts.Token)).Breakpoints;
            Console.WriteLine($"A list: {string.Join(", ", listA.Select(x => $"{x.Id}@{x.Line}"))}");
            await bridgeB.DetachAsync(b, cts.Token);
            var afterDetach = (await bridgeA.ListBreakpointsAsync(a, cts.Token)).Breakpoints;
            var survived = afterDetach.Any(x => x.Id == first.Breakpoint.Id);
            Console.WriteLine($"A's breakpoint survived B's attempt and detach: {survived}");
            return first.Breakpoint.Id == again.Breakpoint.Id && listA.Count == 1 && refused.Length > 0 && survived ? 0 : 1;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
        finally
        {
            if (a is not null)
                await bridgeA.DetachAsync(a, CancellationToken.None);
        }
    }

    /// <summary>audit-must-fix manual check against a throwaway target whose <c>Helper(items, total)</c>
    /// is called from top-level code holding <c>list</c> and a different <c>total</c>: stable ids,
    /// another session's breakpoint listed but not removable, no-side-effects evaluate, frame-true
    /// drill-down, evaluate pinned to f0. Breaks at &lt;file&gt;:&lt;line&gt; inside Helper.</summary>
    public static async Task<int> RunAuditAsync(string[] args)
    {
        var rest = Rest(args, "--live-check-audit");
        if (rest.Length < 3 || !int.TryParse(rest[2], out var line))
            return Usage("usage: VsDebugMcp --live-check-audit <exe> <file> <line>");
        var (exe, file) = (rest[0], rest[1]);
        using var bridge = new DteDebugBridge();
        using var other = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        string? sessionId = null;
        string? otherSession = null;
        var owned = new List<string>();
        var failures = 0;
        try
        {
            sessionId = (await bridge.AttachAsync(null, cts.Token)).SessionId;
            otherSession = (await other.AttachAsync(null, cts.Token)).SessionId;
            var a = (await bridge.SetBreakpointAsync(sessionId, file, 1, cts.Token)).Breakpoint.Id;
            var b = (await bridge.SetBreakpointAsync(sessionId, file, 2, cts.Token)).Breakpoint.Id;
            var c = (await bridge.SetBreakpointAsync(sessionId, file, 4, cts.Token)).Breakpoint.Id;
            owned.AddRange([a, b, c]);
            await other.SetBreakpointAsync(otherSession, file, 3, cts.Token);
            await bridge.RemoveBreakpointAsync(sessionId, a, cts.Token);
            owned.Remove(a);
            await bridge.RemoveBreakpointAsync(sessionId, c, cts.Token);
            owned.Remove(c);
            var list = await bridge.ListBreakpointsAsync(sessionId, cts.Token);
            foreach (var bp in list.Breakpoints)
                Console.WriteLine($"  {bp.Id} {bp.File}:{bp.Line} warning={bp.Warning}");
            Check(ref failures, list.Breakpoints.Count(bp => bp.Id == b && bp.Line == 2) == 1, $"{b} still names line 2 after removing {a} and {c}");
            Check(ref failures, list.Breakpoints.Count(bp => bp.File.Equals(file, StringComparison.OrdinalIgnoreCase) && bp.Line == 2) == 1, "owned breakpoint not duplicated as other<N>");
            var foreign = list.Breakpoints.FirstOrDefault(bp => bp.File.Equals(file, StringComparison.OrdinalIgnoreCase) && bp.Line == 3);
            Check(ref failures, foreign is not null && foreign.Id.StartsWith("other", StringComparison.Ordinal), "other session's breakpoint listed as other<N>");
            if (foreign is not null)
                Check(ref failures, await FailsWith(() => bridge.RemoveBreakpointAsync(sessionId, foreign.Id, cts.Token), "breakpoint-not-found"), "other<N> not removable");
            Check(ref failures, await FailsWith(() => bridge.RemoveBreakpointAsync(sessionId, a, cts.Token), "breakpoint-not-found"), "removed id stays not-found");
            await other.DetachAsync(otherSession, cts.Token);
            otherSession = null;
            await bridge.RemoveBreakpointAsync(sessionId, b, cts.Token);
            owned.Remove(b);

            owned.Add((await bridge.SetBreakpointAsync(sessionId, file, line, cts.Token)).Breakpoint.Id);
            var launch = await bridge.LaunchProcessAsync(sessionId, exe, null, null, null, 30_000, cts.Token);
            Console.WriteLine($"launch: pid={launch.ProcessId} mode={launch.Mode} lastHit={launch.LastHit}");
            if (launch.Mode != "break")
            {
                Console.Error.WriteLine("did not break in Helper");
                return 1;
            }
            var count = await bridge.EvaluateAsync(sessionId, "items.Count", 10_000, cts.Token);
            Check(ref failures, count.Value == "3" && count.Expression == "items.Count", $"nse property read works and echoes input (got {count.Expression} = {count.Value})");
            Check(ref failures, await FailsWith(() => bridge.EvaluateAsync(sessionId, "items.Remove(1)", 10_000, cts.Token), "invalid-expression"), "mutating call refused");
            var after = await bridge.EvaluateAsync(sessionId, "items.Count", 10_000, cts.Token);
            Check(ref failures, after.Value == "3", $"target unchanged after refused call (items.Count = {after.Value})");
            var f1 = await bridge.ReadVariablesAsync(sessionId, "f1", null, 50, cts.Token);
            Console.WriteLine($"  f1: {string.Join(", ", f1.Variables.Select(v => $"{v.Name}={v.Value}"))}");
            Check(ref failures, f1.Variables.Any(v => v.Name == "list"), "f1 top level has list");
            var drilled = await bridge.ReadVariablesAsync(sessionId, "f1", "list", 50, cts.Token);
            Check(ref failures, drilled.Variables.Count > 0, $"f1 drill-down of list resolves in f1 ({drilled.Variables.Count} members)");
            var total = await bridge.EvaluateAsync(sessionId, "total", 10_000, cts.Token);
            Check(ref failures, total.Value == "82", $"evaluate after reading f1 runs in f0 (total = {total.Value}, f0 is 82, f1 is 41)");
            Check(ref failures, await FailsWith(() => bridge.ReadVariablesAsync(sessionId, "f99", "list", 50, cts.Token), "frame-not-found"), "unknown frame with path is frame-not-found");
            foreach (var id in owned.ToList())
            {
                await bridge.RemoveBreakpointAsync(sessionId, id, cts.Token);
                owned.Remove(id);
            }
            var end = await bridge.ResumeAsync(sessionId, 30_000, cts.Token);
            Console.WriteLine($"run to end: mode={end.Mode} note={end.Note}");
            Console.WriteLine(failures == 0 ? "audit live check: all passed" : $"audit live check: {failures} failed");
            return failures == 0 ? 0 : 1;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
        finally
        {
            // Detach deletes every breakpoint each session created: nothing left in the user's VS.
            if (otherSession is not null)
                await other.DetachAsync(otherSession, CancellationToken.None);
            if (sessionId is not null)
                await bridge.DetachAsync(sessionId, CancellationToken.None);
        }
    }

    /// <summary>exception-config manual check against the Sandbox: settings list/set/reject, a
    /// first-chance break carrying type, path and message (matching the $exception.Message probe),
    /// then the setting is restored with default.</summary>
    public static async Task<int> RunExceptionAsync(string[] args)
    {
        var rest = Rest(args, "--live-check-exception");
        var exe = rest.FirstOrDefault();
        if (exe is null)
            return Usage("usage: VsDebugMcp --live-check-exception <Sandbox.exe>");
        const string type = "System.InvalidOperationException";
        using var bridge = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string? sessionId = null;
        var changed = false;
        var failures = 0;
        try
        {
            sessionId = (await bridge.AttachAsync(null, cts.Token)).SessionId;
            var all = await bridge.ListExceptionSettingsAsync(sessionId, null, cts.Token);
            Console.WriteLine($"list: count={all.Settings.Count} mode={all.Mode}");
            var found = all.Settings.FirstOrDefault(s => s.Name == type);
            Check(ref failures, found is not null, $"{type} listed (found mode {found?.Mode})");
            Check(ref failures, await FailsWith(() => bridge.SetExceptionSettingAsync(sessionId, "InvalidOperationException", ExceptionBreakModes.FirstChance, cts.Token), ErrorCodes.ExceptionNotFound), "short name is exception-not-found");
            Check(ref failures, await FailsWith(() => bridge.SetExceptionSettingAsync(sessionId, "System.NoSuchException", ExceptionBreakModes.FirstChance, cts.Token), ErrorCodes.ExceptionNotFound), "unknown name is exception-not-found");
            var set = await bridge.SetExceptionSettingAsync(sessionId, type, ExceptionBreakModes.FirstChance, cts.Token);
            changed = true;
            Check(ref failures, set.Setting.Mode == ExceptionBreakModes.FirstChance, "set first-chance echoes first-chance");

            var launch = await bridge.LaunchProcessAsync(sessionId, exe, "throw", null, null, 30_000, cts.Token);
            Console.WriteLine($"launch: mode={launch.Mode} lastHit={launch.LastHit} note={launch.Note}");
            Console.WriteLine($"  exception: type={launch.Exception?.Type} path={launch.Exception?.Path} message={launch.Exception?.Message ?? "(null)"}");
            Check(ref failures, launch.Mode == "break" && launch.LastHit is null, "first-chance break with no lastHit");
            Check(ref failures, launch.Exception?.Type.Contains(type, StringComparison.Ordinal) == true, "payload type");
            Check(ref failures, launch.Exception?.Path == "$exception", "payload path");
            if (launch.Exception is { Message: { } message })
            {
                var probe = await bridge.EvaluateAsync(sessionId, "$exception.Message", 10_000, cts.Token);
                Check(ref failures, probe.Value == message, $"message matches probe ({probe.Value})");
            }
            else
            {
                Console.WriteLine("OBS message null: nse refused the getter; record in TROUBLESHOOTING.md");
            }
            for (var i = 0; i < 3; i++)
            {
                var next = await bridge.ResumeAsync(sessionId, 30_000, cts.Token);
                Console.WriteLine($"resume: mode={next.Mode} note={next.Note}");
                if (next.Mode != "break") break;
            }
            Console.WriteLine(failures == 0 ? "exception live check: all passed" : $"exception live check: {failures} failed");
            return failures == 0 ? 0 : 1;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
        finally
        {
            // Settings live in the VS instance (ADR-0011): restore what this check changed.
            // Detach is guaranteed even when restore fails; a failed restore counts, never hides.
            if (sessionId is not null)
            {
                if (changed)
                {
                    try
                    {
                        await bridge.SetExceptionSettingAsync(sessionId, type, ExceptionBreakModes.Default, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        Console.WriteLine($"FAIL restore {type} to default: {ex.Message}");
                    }
                }
                try
                {
                    await bridge.DetachAsync(sessionId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"detach failed: {ex.Message}");
                }
            }
        }
    }

    private static string[] Rest(string[] args, string flag) =>
        args.SkipWhile(a => a != flag).Skip(1).ToArray();

    private static int Usage(string line)
    {
        Console.Error.WriteLine(line);
        return 2;
    }

    private static int ReportDomain(DebugDomainException ex)
    {
        Console.Error.WriteLine($"DOMAIN {ex.Code}: {ex.Message}");
        return 1;
    }

    private static void Check(ref int failures, bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {what}");
        if (!ok) failures++;
    }

    private static async Task<bool> FailsWith(Func<Task> call, string code)
    {
        try
        {
            await call();
            return false;
        }
        catch (DebugDomainException ex)
        {
            Console.WriteLine($"  -> {ex.Code}: {ex.Message}");
            return ex.Code == code;
        }
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var rest = Rest(args, "--live-check");
        var file = rest.FirstOrDefault();
        var lineText = rest.Skip(1).FirstOrDefault();
        if (file is null || !int.TryParse(lineText, out var line))
            return Usage("usage: VsDebugMcp --live-check <file> <line>");
        using var bridge = new DteDebugBridge();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var attach = await bridge.AttachAsync(null, cts.Token);
            Console.WriteLine($"attach ok: session={attach.SessionId} mode={attach.Mode} lastHit={attach.LastHit}");
            var set = await bridge.SetBreakpointAsync(attach.SessionId, file, line, cts.Token);
            Console.WriteLine($"set ok: id={set.Breakpoint.Id} bound={set.Breakpoint.Bound} warning={set.Breakpoint.Warning}");
            var list = await bridge.ListBreakpointsAsync(attach.SessionId, cts.Token);
            Console.WriteLine($"list ok: count={list.Breakpoints.Count}");
            foreach (var bp in list.Breakpoints)
                Console.WriteLine($"  {bp.Id} {bp.File}:{bp.Line} bound={bp.Bound}");
            var removed = await bridge.RemoveBreakpointAsync(attach.SessionId, set.Breakpoint.Id, cts.Token);
            Console.WriteLine($"remove ok: {removed.RemovedId}");
            return 0;
        }
        catch (DebugDomainException ex)
        {
            return ReportDomain(ex);
        }
    }
}
