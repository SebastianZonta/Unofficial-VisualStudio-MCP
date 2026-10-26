using ModelContextProtocol;
using VsDebugMcp;

// Seam 1 (tool-call boundary): drive ExecutionTools against the fake bridge.
// Seam 2 (bridge interface): mode transitions + timeout-as-data underneath.
public sealed class ExecutionTests
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
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        return (bridge, sessionId);
    }

    [Fact]
    public async Task Resume_with_armed_breakpoint_lands_in_break_with_last_hit()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("break", resumed.Mode);
        Assert.Equal($"{set.Breakpoint.File}:{set.Breakpoint.Line}", resumed.LastHit);
        Assert.Equal("ok", resumed.Status);
    }

    [Fact]
    public async Task Pause_transitions_run_to_break()
    {
        var (bridge, sessionId) = await AttachedAsync(); // no breakpoints: resume stays running
        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        Assert.Equal("run", resumed.Mode);

        var paused = await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);

        Assert.Equal("break", paused.Mode);
        Assert.Equal("ok", paused.Status);
    }

    [Fact]
    public async Task Step_moves_in_break_mode_with_direction_echo()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var stepped = await ExecutionTools.StepAsync(bridge, sessionId, "over", 5000, CancellationToken.None);

        Assert.Equal("break", stepped.Mode);
        Assert.Equal("ok", stepped.Status);
        Assert.Contains("over", stepped.Note);
    }

    [Fact]
    public async Task Step_outside_break_mode_fails_named()
    {
        var (bridge, sessionId) = await AttachedAsync(); // design mode

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.StepAsync(bridge, sessionId, "over", 5000, CancellationToken.None));
        Assert.Contains("not-in-break-mode", ex.Message);
    }

    [Fact]
    public async Task Step_timeout_is_data_not_a_hang()
    {
        var (bridge, sessionId) = await BrokenAsync();
        bridge.ForceTimeout = true;

        var stepped = await ExecutionTools.StepAsync(bridge, sessionId, "into", 5000, CancellationToken.None);

        Assert.Equal("timeout", stepped.Status);
        Assert.Equal("break", stepped.Mode); // mode unchanged, agent can retry or pause
    }

    [Fact]
    public async Task Resume_timeout_is_data_not_a_hang()
    {
        var (bridge, sessionId) = await AttachedAsync();
        bridge.ForceTimeout = true;

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("timeout", resumed.Status);
        Assert.Equal("design", resumed.Mode); // mode unchanged, dispatcher still usable

        bridge.ForceTimeout = false;
        var retried = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        Assert.Equal("ok", retried.Status); // dispatcher was never wedged
    }

    [Fact]
    public async Task Step_rejects_bad_direction_without_touching_bridge()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.StepAsync(bridge, sessionId, "sideways", 5000, CancellationToken.None));
        Assert.Contains("invalid-argument", ex.Message);
    }

    [Fact]
    public async Task Pause_when_not_running_fails_named()
    {
        var (bridge, sessionId) = await AttachedAsync(); // design mode, nothing running

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None));
        Assert.Contains("not-running", ex.Message);
    }

    [Fact]
    public async Task Unknown_session_fails_named_no_session()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            ExecutionTools.ResumeAsync(bridge, "bogus-session-id", 5000, CancellationToken.None));
        Assert.Contains(ErrorCodes.NoSession, ex.Message);
    }
}