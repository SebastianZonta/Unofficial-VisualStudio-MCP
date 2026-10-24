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
        var before = existing.Breakpoints.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        // Reuse only a breakpoint at exactly this destination: the shared suffix
        // match would let a same-named file elsewhere hijack the run.
        if (existing.Breakpoints.FirstOrDefault(b => ExactLocation(b.File, b.Line, file, line)) is { } reuse)
        {
            if (!reuse.Bound)
                throw new DebugDomainException(
                    ErrorCodes.UnboundLocation, $"{file}:{line} did not bind: {reuse.Warning}",
                    "check the file path and line, or list_modules for the module's symbol state");
            return await ContinueAsync(cont, existing.Mode);
        }
        var set = await bridge.SetBreakpointAsync(sessionId, file, line, ct);
        // Removal is conditional on creation, decided before the run: only what
        // this call created is removed on the way out.
        var created = set.Created && !before.Contains(set.Breakpoint.Id);
        try
        {
            if (!set.Breakpoint.Bound)
                throw new DebugDomainException(
                    ErrorCodes.UnboundLocation, $"{file}:{line} did not bind: {set.Breakpoint.Warning}",
                    "check the file path and line, or list_modules for the module's symbol state");
            return await ContinueAsync(cont, set.Mode);
        }
        finally
        {
            if (created)
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
    }

    /// <summary>A resume that lands while the target already runs joins the run
    /// instead of failing: a concurrent resume between placement and continue
    /// is not this call's error.</summary>
    private static async Task<ExecutionResult> ContinueAsync(
        Func<string, Task<ExecutionResult>> cont, string mode)
    {
        try
        {
            return await cont(mode);
        }
        catch (DebugDomainException ex) when (ex.Code == ErrorCodes.AlreadyRunning)
        {
            return await cont("run");
        }
    }

    /// <summary>Exact file identity for destination reuse: same matching as the
    /// shared suffix rule but without the suffix, so a same-named file elsewhere
    /// never counts as this destination.</summary>
    private static bool ExactLocation(string listedFile, int listedLine, string file, int line)
    {
        if (listedLine != line)
            return false;
        var listed = listedFile.Replace('/', '\\').TrimStart('\\');
        var wanted = file.Replace('/', '\\').TrimStart('\\');
        return listed.Equals(wanted, StringComparison.OrdinalIgnoreCase);
    }
}
