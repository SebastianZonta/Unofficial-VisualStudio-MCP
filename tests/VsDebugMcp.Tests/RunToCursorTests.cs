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
