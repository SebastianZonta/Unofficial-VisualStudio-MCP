using System.Runtime.InteropServices;
using ModelContextProtocol;
using VsDebugMcp;

// Tool-call boundary: run_to_cursor against the fake bridge. The temporary
// breakpoint is observable only through list_breakpoints, never by id.
public sealed class RunToCursorTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    private static async Task<(FakeDebugBridge Bridge, string SessionId)> BrokenAsync()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 10, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        await BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, set.Breakpoint.Id, CancellationToken.None);
        return (bridge, sessionId);
    }

    private static async Task<int> BreakpointCountAsync(FakeDebugBridge bridge, string sessionId) =>
        (await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints.Count;

    [Fact]
    public async Task Arrival_reports_destination_and_leaves_no_breakpoint()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("break", run.Mode);
        Assert.Equal("ok", run.Status);
        Assert.Contains("Order.cs:20", run.LastHit);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Run_mode_is_accepted()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None); // no breakpoints: running

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("break", run.Mode);
        Assert.Contains("Order.cs:20", run.LastHit);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Timeout_reports_status_and_cleans_up()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.ForceTimeout = true;

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("timeout", run.Status);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Unbindable_location_fails_fast_and_cleans_up()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.SymbolsLoaded = false;

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.RunToCursorAsync(bridge, sessionId, "Nope.cs", 1, 5000, CancellationToken.None));

        Assert.Contains(ErrorCodes.UnboundLocation, ex.Message);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Design_mode_is_rejected_and_leaves_no_breakpoint()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None));

        Assert.Contains(ErrorCodes.NotRunning, ex.Message);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Earlier_user_hit_wins_and_user_breakpoint_survives()
    {
        var (bridge, sessionId) = await BrokenAsync();
        var mine = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 15, CancellationToken.None);

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Contains($"{mine.Breakpoint.Id} Program.cs:15", run.LastHit);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal(mine.Breakpoint.Id, Assert.Single(list.Breakpoints).Id);
    }

    [Fact]
    public async Task Breakpoint_already_at_destination_is_reused_and_survives()
    {
        var (bridge, sessionId) = await BrokenAsync();
        var mine = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None);

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Contains($"{mine.Breakpoint.Id} Order.cs:20", run.LastHit);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal(mine.Breakpoint.Id, Assert.Single(list.Breakpoints).Id);
    }

    [Fact]
    public async Task Busy_vs_on_cleanup_is_retried_until_the_temporary_breakpoint_is_gone()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.RemoveBusyFailures = 2;

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Contains("Order.cs:20", run.LastHit);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task Persistently_busy_cleanup_keeps_the_breakpoint_owned_and_removable()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.RemoveBusyFailures = 10;

        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("break", run.Mode); // the run's own result is never masked
        var left = Assert.Single((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
        Assert.StartsWith("bp", left.Id); // still owned, not other<N>
        bridge.RemoveBusyFailures = 0;
        await BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, left.Id, CancellationToken.None);
        Assert.Equal(0, await BreakpointCountAsync(bridge, sessionId));
    }

    [Fact]
    public async Task RunToCursor_joins_run_on_go_conflict()
    {
        var inner = new FakeDebugBridge();
        var sessionId = (await SessionTools.AttachAsync(inner, instanceOverride: null, CancellationToken.None)).SessionId;
        await BreakpointTools.SetBreakpointAsync(inner, sessionId, "Program.cs", 10, CancellationToken.None);
        await ExecutionTools.ResumeAsync(inner, sessionId, 5000, CancellationToken.None);
        await BreakpointTools.RemoveBreakpointAsync(inner, sessionId, "bp1", CancellationToken.None);
        var bridge = new GoConflictOnceBridge(inner, unchecked((int)0x89711007));

        // Temp is placed in break mode; the concurrent resume's Go wins before
        // the continue lands, so Go is rejected with 0x89711007. The run
        // re-reads state and joins the wait instead of failing (E27).
        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("break", run.Mode);
        Assert.Equal("ok", run.Status);
        Assert.Contains("Order.cs:20", run.LastHit);
        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task RunToCursor_does_not_join_other_automation_failures()
    {
        var inner = new FakeDebugBridge();
        var sessionId = (await SessionTools.AttachAsync(inner, instanceOverride: null, CancellationToken.None)).SessionId;
        await BreakpointTools.SetBreakpointAsync(inner, sessionId, "Program.cs", 10, CancellationToken.None);
        await ExecutionTools.ResumeAsync(inner, sessionId, 5000, CancellationToken.None);
        await BreakpointTools.RemoveBreakpointAsync(inner, sessionId, "bp1", CancellationToken.None);
        var bridge = new GoConflictOnceBridge(inner, unchecked((int)0x80004005));

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None));

        Assert.Contains("automation-failed", ex.Message);
        Assert.Equal(0, await BreakpointCountAsync(inner, sessionId));
    }
    [Fact]
    public async Task RunToCursor_joins_run_when_started_after_set()
    {
        var inner = new FakeDebugBridge();
        var sessionId = (await SessionTools.AttachAsync(inner, instanceOverride: null, CancellationToken.None)).SessionId;
        await BreakpointTools.SetBreakpointAsync(inner, sessionId, "Program.cs", 10, CancellationToken.None);
        await ExecutionTools.ResumeAsync(inner, sessionId, 5000, CancellationToken.None);
        await BreakpointTools.RemoveBreakpointAsync(inner, sessionId, "bp1", CancellationToken.None);
        var bridge = new AlreadyRunningOnceBridge(inner);

        // Temp is placed in break mode; the target starts running before the
        // continue lands. The run joins the wait instead of failing.
        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("break", run.Mode);
        Assert.Equal("ok", run.Status);
        Assert.Contains("Order.cs:20", run.LastHit);
        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task RunToCursor_never_deletes_preexisting()
    {
        var inner = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(inner, instanceOverride: null, CancellationToken.None);
        var bridge = new SeedOnSetBridge(inner, "Order.cs", 20);
        var sessionId = attach.SessionId;
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 10, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        await BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, "bp1", CancellationToken.None);

        // A concurrent set wins the List-then-Set race: the run reuses the
        // seeded breakpoint and leaves it listed.
        var run = await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        Assert.Equal("break", run.Mode);
        Assert.Contains("Order.cs:20", run.LastHit);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        var survivor = Assert.Single(list.Breakpoints);
        Assert.Contains("Order.cs:20", $"{survivor.File}:{survivor.Line}");
    }

    [Fact]
    public async Task RunToCursor_reuse_requires_bound()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.SymbolsLoaded = false;
        var seeded = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None);
        Assert.False(seeded.Breakpoint.Bound);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None));

        Assert.Contains(ErrorCodes.UnboundLocation, ex.Message);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal(seeded.Breakpoint.Id, Assert.Single(list.Breakpoints).Id);
    }

    [Theory]
    [InlineData(@"C:\src\Shop\Order.cs", "Order.cs", true)]
    [InlineData(@"C:\src\Shop\Order.cs", "Shop/Order.cs", true)]
    [InlineData(@"C:\src\Shop\Order.cs", @"C:\SRC\Shop\Order.cs", true)]
    [InlineData(@"C:\src\Shop\MyOrder.cs", "Order.cs", false)]
    public void Destination_matches_listed_full_path(string listed, string passed, bool same)
    {
        Assert.Equal(same, BreakpointOptions.SameLocation(listed, 20, passed, 20));
        Assert.False(BreakpointOptions.SameLocation(listed, 20, passed, 21));
    }

    [Fact]
    public async Task Sibling_session_is_not_disturbed()
    {
        var (bridge, sessionId) = await BrokenAsync();
        var other = await SessionTools.AttachAsync(bridge, "other", CancellationToken.None);

        await ExecutionTools.RunToCursorAsync(bridge, sessionId, "Order.cs", 20, 5000, CancellationToken.None);

        var otherList = await BreakpointTools.ListBreakpointsAsync(bridge, other.SessionId, CancellationToken.None);
        Assert.Equal("design", otherList.Mode);
        Assert.Empty(otherList.Breakpoints);
    }
}

/// <summary>Pass-through decorator over the fake bridge for List-then-Set race
/// tests. Subclasses override one seam; everything else delegates.</summary>
internal class DelegatingBridge(FakeDebugBridge inner) : IDebugBridge
{
    protected FakeDebugBridge Inner { get; } = inner;

    public virtual Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken ct) => Inner.AttachAsync(instanceOverride, ct);
    public virtual Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct) => Inner.ListInstancesAsync(ct);
    public virtual Task<ListSessionsResult> ListSessionsAsync(CancellationToken ct) => Inner.ListSessionsAsync(ct);
    public virtual Task<DetachResult> DetachAsync(string sessionId, CancellationToken ct) => Inner.DetachAsync(sessionId, ct);
    public virtual Task<ListProcessesResult> ListProcessesAsync(string? nameFilter, bool ownedOnly, CancellationToken ct) => Inner.ListProcessesAsync(nameFilter, ownedOnly, ct);
    public virtual Task RequireSessionAsync(string sessionId, CancellationToken ct) => Inner.RequireSessionAsync(sessionId, ct);
    public virtual Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct, string? condition = null, int? hitCount = null, string? hitCountMode = null, string? logMessage = null) => Inner.SetBreakpointAsync(sessionId, file, line, ct, condition, hitCount, hitCountMode, logMessage);
    public virtual Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct) => Inner.ListBreakpointsAsync(sessionId, ct);
    public virtual Task<ListModulesResult> ListModulesAsync(string sessionId, string? nameFilter, CancellationToken ct) => Inner.ListModulesAsync(sessionId, nameFilter, ct);
    public virtual Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct) => Inner.RemoveBreakpointAsync(sessionId, breakpointId, ct);
    public virtual Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct) => Inner.PauseAsync(sessionId, ct);
    public virtual Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct) => Inner.ResumeAsync(sessionId, timeoutMs, ct);
    public virtual Task<ExecutionResult> RunToCursorAsync(string sessionId, string file, int line, int timeoutMs, CancellationToken ct) =>
        RunToCursor.RunAsync(this, sessionId, file, line, async mode =>
            mode == "break" ? await ResumeAsync(sessionId, timeoutMs, ct) : await Inner.ResumeAsync(sessionId, timeoutMs, ct), ct);
    public virtual Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct) => Inner.StepAsync(sessionId, direction, timeoutMs, ct);
    public virtual Task<ReadStackResult> ReadStackAsync(string sessionId, int maxFrames, CancellationToken ct) => Inner.ReadStackAsync(sessionId, maxFrames, ct);
    public virtual Task<ReadVariablesResult> ReadVariablesAsync(string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct) => Inner.ReadVariablesAsync(sessionId, frameId, path, maxVariables, ct);
    public virtual Task<SetVariableResult> SetVariableAsync(string sessionId, string frameId, string path, string value, bool consent, CancellationToken ct) => Inner.SetVariableAsync(sessionId, frameId, path, value, consent, ct);
    public virtual Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct) => Inner.EvaluateAsync(sessionId, expression, timeoutMs, ct);
    public virtual Task<ExecutionResult> AttachProcessAsync(string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct) => Inner.AttachProcessAsync(sessionId, processId, consent, timeoutMs, ct);
    public virtual Task<LaunchProcessResult> LaunchProcessAsync(string sessionId, string executablePath, string? arguments, string? workingDirectory, IReadOnlyDictionary<string, string>? environment, int timeoutMs, CancellationToken ct) => Inner.LaunchProcessAsync(sessionId, executablePath, arguments, workingDirectory, environment, timeoutMs, ct);
    public virtual Task<ListExceptionSettingsResult> ListExceptionSettingsAsync(string sessionId, string? nameFilter, CancellationToken ct) => Inner.ListExceptionSettingsAsync(sessionId, nameFilter, ct);
    public virtual Task<SetExceptionSettingResult> SetExceptionSettingAsync(string sessionId, string exceptionType, string mode, CancellationToken ct) => Inner.SetExceptionSettingAsync(sessionId, exceptionType, mode, ct);
}

/// <summary>Simulates the target starting to run between temp placement and
/// continue: the first resume reports already-running, like live.</summary>
internal sealed class AlreadyRunningOnceBridge(FakeDebugBridge inner) : DelegatingBridge(inner)
{
    private bool _raced;

    public override Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct)
    {
        if (!_raced)
        {
            _raced = true;
            throw new DebugDomainException("already-running", "debuggee is already running", "pause first to inspect");
        }
        return Inner.ResumeAsync(sessionId, timeoutMs, ct);
    }
}

/// <summary>Simulates the concurrent resume's Go winning between temp placement
/// and continue: the first resume is rejected with the live Go-conflict
/// HRESULT, like VS does (E27).</summary>
internal sealed class GoConflictOnceBridge(FakeDebugBridge inner, int hresult) : DelegatingBridge(inner)
{
    private readonly int _hresult = hresult;
    private bool _raced;

    public override Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct)
    {
        if (!_raced)
        {
            _raced = true;
            throw new DebugDomainException("automation-failed",
                $"VS automation call failed (HRESULT 0x{_hresult:X8}): Unable to execute method at this time",
                "dismiss any modal dialog in VS and retry",
                new COMException("Unable to execute method at this time", _hresult));
        }
        return Inner.ResumeAsync(sessionId, timeoutMs, ct);
    }
}

/// <summary>Seeds an owned breakpoint at the destination between the run's List
/// and Set, like a concurrent set winning the race.</summary>
internal sealed class SeedOnSetBridge(FakeDebugBridge inner, string file, int line) : DelegatingBridge(inner)
{
    private readonly string _file = file;
    private readonly int _line = line;
    private bool _seeded;

    public override async Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct, string? condition = null, int? hitCount = null, string? hitCountMode = null, string? logMessage = null)
    {
        if (!_seeded && file == _file && line == _line)
        {
            _seeded = true;
            _ = await Inner.SetBreakpointAsync(sessionId, file, line, ct);
        }
        return await Inner.SetBreakpointAsync(sessionId, file, line, ct, condition, hitCount, hitCountMode, logMessage);
    }
}
