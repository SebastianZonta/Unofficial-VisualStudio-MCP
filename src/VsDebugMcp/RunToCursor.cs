namespace VsDebugMcp;

/// <summary>Shared temporary-breakpoint lifecycle for run_to_cursor: place a temporary breakpoint,
/// continue, and remove it on every exit path. Only the breakpoint this call created is touched.</summary>
internal static class RunToCursor
{
    private const int RemoveAttempts = 4;
    private const int RemoveRetryDelayMs = 250;

    /// <param name="cont">Continues from the given mode (break: resume; run: wait for the next stop).</param>
    public static async Task<ExecutionResult> RunAsync(IDebugBridge bridge, string sessionId, string file, int line,
        Func<string, Task<ExecutionResult>> cont, CancellationToken ct)
    {
        // Mode check before any side effect: design mode never sees a temporary breakpoint.
        var existing = await bridge.ListBreakpointsAsync(sessionId, ct);
        if (existing.Mode is not ("break" or "run"))
            throw new DebugDomainException(
                ErrorCodes.NotRunning, $"not debugging: debugger is {existing.Mode}",
                "attach_process or launch_process first; run_to_cursor needs break or run mode");
        // VS hands back the existing breakpoint for a duplicate location, so deleting the
        // "temporary" one would delete the user's: reuse it as the destination instead.
        if (existing.Breakpoints.Any(b => SameLocation(b, file, line)))
            return await cont(existing.Mode);
        var set = await bridge.SetBreakpointAsync(sessionId, file, line, ct);
        try
        {
            if (!set.Breakpoint.Bound)
                throw new DebugDomainException(
                    ErrorCodes.UnboundLocation, $"{file}:{line} did not bind: {set.Breakpoint.Warning}",
                    "check the file path and line, or list_modules for the module's symbol state");
            return await cont(set.Mode);
        }
        finally
        {
            // VS is often busy right after a launch or a hit (seen live as vs-busy): retry a few times.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    // Uncancellable: a cancelled call must still not leave the temporary breakpoint behind.
                    await bridge.RemoveBreakpointAsync(sessionId, set.Breakpoint.Id, CancellationToken.None);
                    break;
                }
                catch (DebugDomainException ex) when (ex.Code == "vs-busy" && attempt < RemoveAttempts)
                {
                    await Task.Delay(RemoveRetryDelayMs, CancellationToken.None);
                }
                catch (Exception ex) when (ex is DebugDomainException or TimeoutException or OperationCanceledException)
                {
                    // Already gone (deleted by hand), the VS instance died, or still busy: the id stays
                    // owned, so remove_breakpoint can retry. Never mask the run's own result.
                    break;
                }
            }
        }
    }

    /// <summary>Listed paths are VS full paths; the caller may pass the path as shown in the solution.
    /// shortcut: suffix match, so a same-named file elsewhere also counts; upgrade if that bites.</summary>
    internal static bool SameLocation(BreakpointInfo bp, string file, int line)
    {
        if (bp.Line != line)
            return false;
        var tail = file.Replace('/', '\\').TrimStart('\\');
        return bp.File.Equals(file, StringComparison.OrdinalIgnoreCase)
            || bp.File.Replace('/', '\\').EndsWith("\\" + tail, StringComparison.OrdinalIgnoreCase);
    }
}
