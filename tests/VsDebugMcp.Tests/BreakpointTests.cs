using ModelContextProtocol;
using VsDebugMcp;

// Seam 1 (tool-call boundary): drive BreakpointTools against the fake bridge.
// Seam 2 (bridge interface): bind lifecycle + session gating underneath.
public sealed class BreakpointTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    [Fact]
    public async Task Set_returns_id_bound_flag_mode_and_last_hit()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var result = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Breakpoint.Id));
        Assert.True(result.Breakpoint.Bound);
        Assert.Null(result.Breakpoint.Warning);
        Assert.False(string.IsNullOrWhiteSpace(result.Mode));
        Assert.True(result.GetType().GetProperty("LastHit") is not null);
    }

    [Fact]
    public async Task Unbound_warning_then_poll_to_confirm_binding()
    {
        var (bridge, sessionId) = await AttachedAsync();
        bridge.SymbolsLoaded = false;

        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 17, CancellationToken.None);
        Assert.False(set.Breakpoint.Bound);
        Assert.Contains("symbols", set.Breakpoint.Warning, StringComparison.OrdinalIgnoreCase);

        bridge.SymbolsLoaded = true;
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        var polled = Assert.Single(list.Breakpoints, b => b.Id == set.Breakpoint.Id);
        Assert.True(polled.Bound);
    }

    [Fact]
    public async Task Remove_by_id_drops_the_breakpoint()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);

        var removed = await BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, set.Breakpoint.Id, CancellationToken.None);
        Assert.Equal(set.Breakpoint.Id, removed.RemovedId);

        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.DoesNotContain(list.Breakpoints, b => b.Id == set.Breakpoint.Id);
    }

    [Fact]
    public async Task Remove_unknown_id_fails_named()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, "bp-nope", CancellationToken.None));
        Assert.Contains("breakpoint-not-found", ex.Message);
    }

    [Fact]
    public async Task Unknown_session_fails_named_no_session()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, "bogus-session-id", "Program.cs", 42, CancellationToken.None));
        Assert.Contains("no-session", ex.Message);
    }

    [Fact]
    public async Task Invalid_line_fails_named_without_touching_bridge()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 0, CancellationToken.None));
        Assert.Contains("invalid-argument", ex.Message);
    }
}
