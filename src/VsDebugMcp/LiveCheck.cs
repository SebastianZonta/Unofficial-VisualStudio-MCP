namespace VsDebugMcp;

/// <summary>Builder-only harness for the ticket-11 manual check: attach + set + list
/// against the real VS 2026, print results, exit. Not an MCP tool.</summary>
internal static class LiveCheck
{
    public static async Task<int> RunFullAsync(string[] args)
    {
        var file = args.SkipWhile(a => a != "--live-check-full").Skip(1).FirstOrDefault();
        var lineText = args.SkipWhile(a => a != "--live-check-full").Skip(2).FirstOrDefault();
        if (file is null || !int.TryParse(lineText, out var line))
        {
            Console.Error.WriteLine("usage: VsDebugMcp --live-check-full <file> <line>");
            return 2;
        }
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
            Console.Error.WriteLine($"DOMAIN {ex.Code}: {ex.Message}");
            return 1;
        }
    }

    /// <summary>launch_process manual check: entry-point breakpoint binds at startup, and
    /// args (a1 a2), working directory (the temp dir) and env (LIVE_CHECK=launched) pass through.</summary>
    public static async Task<int> RunLaunchAsync(string[] args)
    {
        var rest = args.SkipWhile(a => a != "--live-check-launch").Skip(1).ToArray();
        if (rest.Length < 3 || !int.TryParse(rest[2], out var line))
        {
            Console.Error.WriteLine("usage: VsDebugMcp --live-check-launch <exe> <file> <line>");
            return 2;
        }
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
                // Func-eval right after startup can be rejected while VS is still busy: retry vs-busy.
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        var eval = await bridge.EvaluateAsync(sessionId, expr, 10_000, cts.Token);
                        Console.WriteLine($"  {expr} = {eval.Value}");
                        break;
                    }
                    catch (DebugDomainException ex) when (ex.Code == "vs-busy" && attempt < 10)
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                }
            }
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
            Console.Error.WriteLine($"DOMAIN {ex.Code}: {ex.Message}");
            return 1;
        }
        finally
        {
            if (sessionId is not null && bpId is not null)
                await bridge.RemoveBreakpointAsync(sessionId, bpId, CancellationToken.None);
        }
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var file = args.SkipWhile(a => a != "--live-check").Skip(1).FirstOrDefault();
        var lineText = args.SkipWhile(a => a != "--live-check").Skip(2).FirstOrDefault();
        if (file is null || !int.TryParse(lineText, out var line))
        {
            Console.Error.WriteLine("usage: VsDebugMcp --live-check <file> <line>");
            return 2;
        }
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
            Console.Error.WriteLine($"DOMAIN {ex.Code}: {ex.Message}");
            return 1;
        }
    }
}
