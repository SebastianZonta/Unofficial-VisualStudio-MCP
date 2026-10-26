using VsDebugMcp;

// Seam 2 (bridge interface): several sessions live side by side, isolated;
// detach and VS-exit strike exactly one session.
public sealed class MultiSessionTests
{
    [Fact]
    public async Task Distinct_instance_overrides_get_distinct_sessions()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);
        var b = await bridge.AttachAsync("B", CancellationToken.None);

        Assert.NotEqual(a.SessionId, b.SessionId);
    }

    [Fact]
    public async Task Breakpoints_are_isolated_per_session()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);
        var b = await bridge.AttachAsync("B", CancellationToken.None);

        await bridge.SetBreakpointAsync(a.SessionId, "A.cs", 1, CancellationToken.None);

        Assert.Empty((await bridge.ListBreakpointsAsync(b.SessionId, CancellationToken.None)).Breakpoints);
        Assert.Single((await bridge.ListBreakpointsAsync(a.SessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Mode_is_isolated_per_session()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);
        var b = await bridge.AttachAsync("B", CancellationToken.None);

        Assert.Equal("run", (await bridge.ResumeAsync(a.SessionId, 5000, CancellationToken.None)).Mode);

        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.PauseAsync(b.SessionId, CancellationToken.None));
        Assert.Contains("not-running", ex.Message);
    }

    [Fact]
    public async Task Detach_forgets_one_session_and_keeps_siblings()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);
        var b = await bridge.AttachAsync("B", CancellationToken.None);

        var detached = await SessionTools.DetachAsync(bridge, a.SessionId, CancellationToken.None);
        Assert.Equal(a.SessionId, detached.SessionId);

        var forgotten = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.RequireSessionAsync(a.SessionId, CancellationToken.None));
        Assert.Contains(ErrorCodes.NoSession, forgotten.Message);

        await bridge.RequireSessionAsync(b.SessionId, CancellationToken.None);
        var sessions = await SessionTools.ListSessionsAsync(bridge, CancellationToken.None);
        var only = Assert.Single(sessions.Sessions);
        Assert.Equal(b.SessionId, only.SessionId);
    }

    [Fact]
    public async Task Detach_unknown_session_fails_with_no_session()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.DetachAsync("bogus-session-id", CancellationToken.None));
        Assert.Contains(ErrorCodes.NoSession, ex.Message);
    }

    [Fact]
    public async Task Vs_exit_reports_per_session_vs_not_running()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);
        var b = await bridge.AttachAsync("B", CancellationToken.None);

        bridge.SimulateVsExit(a.SessionId);

        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.SetBreakpointAsync(a.SessionId, "A.cs", 1, CancellationToken.None));
        Assert.Contains(ErrorCodes.VsNotRunning, ex.Message);

        await bridge.RequireSessionAsync(b.SessionId, CancellationToken.None);
    }

    [Fact]
    public async Task Reattach_after_vs_exit_binds_fresh_session()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);

        bridge.SimulateVsExit(a.SessionId);
        var fresh = await bridge.AttachAsync("A", CancellationToken.None);

        Assert.NotEqual(a.SessionId, fresh.SessionId);
        await bridge.RequireSessionAsync(fresh.SessionId, CancellationToken.None);
    }
}