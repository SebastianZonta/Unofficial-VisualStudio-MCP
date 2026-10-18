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
