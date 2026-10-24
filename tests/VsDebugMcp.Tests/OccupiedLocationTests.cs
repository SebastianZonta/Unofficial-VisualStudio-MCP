using ModelContextProtocol;
using VsDebugMcp;

// set_breakpoint on a location that already holds a breakpoint: VS would hand that
// breakpoint back from Add, so the bridge refuses foreign ones and repeats own ones.
public sealed class OccupiedLocationTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    [Fact]
    public async Task Foreign_breakpoint_location_is_refused_and_left_alone()
    {
        var (bridge, sessionId) = await AttachedAsync();
        bridge.AddForeignBreakpoint(sessionId, @"C:\src\Shop\Order.cs", 20);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None));

        Assert.Contains(ErrorCodes.BreakpointExists, ex.Message);
        Assert.Contains("other1", ex.Message);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal("other1", Assert.Single(list.Breakpoints).Id);
    }

    [Fact]
    public async Task Own_repeat_returns_the_same_id_without_a_second_breakpoint()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var first = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None, condition: "total > 0");

        var again = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None, condition: "total > 0");

        Assert.Equal(first.Breakpoint.Id, again.Breakpoint.Id);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Single(list.Breakpoints);
    }

    [Fact]
    public async Task Own_repeat_with_different_options_is_refused_and_keeps_the_original()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var first = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Order.cs", 20, CancellationToken.None, hitCount: 3));

        Assert.Contains(ErrorCodes.InvalidArgument, ex.Message);
        Assert.Contains(first.Breakpoint.Id, ex.Message);
        var only = Assert.Single((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
        Assert.Null(only.HitCountTarget);
    }
}
