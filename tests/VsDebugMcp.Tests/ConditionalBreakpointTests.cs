using ModelContextProtocol;
using VsDebugMcp;

// Conditional breakpoints (spec .scratch/conditional-breakpoints): tool-boundary
// tests against the fake bridge. Fake condition truthiness: "false"/"0" is
// false, anything else true. Log template {expr} renders the canned value.
public sealed class ConditionalBreakpointTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    [Fact]
    public async Task Write_like_condition_fails_named_with_nothing_created()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, condition: "order.Total = 0"));

        Assert.Contains("invalid-argument", ex.Message);
        Assert.Contains("condition", ex.Message);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Empty(list.Breakpoints);
    }

    [Fact]
    public async Task Bad_hit_count_and_mode_fail_naming_the_input()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var zero = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, hitCount: 0));
        Assert.Contains("hitCount", zero.Message);

        var mode = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, hitCount: 3, hitCountMode: "sometimes"));
        Assert.Contains("hitCountMode", mode.Message);

        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Logpoint_rejects_stopping_combo_and_write_like_holes()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var combo = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
                condition: "total > 0", logMessage: "total={total}"));
        Assert.Contains("logMessage", combo.Message);

        var hole = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
                logMessage: "total={total = 0}"));
        Assert.Contains("logMessage", hole.Message);

        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Bridge_rejects_write_like_condition_before_mutation()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            bridge.SetBreakpointAsync(sessionId, "Program.cs", 42, CancellationToken.None, condition: "a; b"));

        Assert.Contains("invalid-argument", ex.Message);
        Assert.Empty((await bridge.ListBreakpointsAsync(sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Logpoint_shape_edges_fail_naming_log_message()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var lonely = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
                hitCountMode: HitCountModes.Every));
        Assert.Contains("hitCountMode", lonely.Message);

        var empty = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
                logMessage: "   "));
        Assert.Contains("logMessage", empty.Message);

        var unbalanced = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
                logMessage: "total={total"));
        Assert.Contains("logMessage", unbalanced.Message);

        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Logpoint_never_stops_and_buffers_readable_log()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
            logMessage: "total={total}");
        Assert.Equal("total={total}", set.Breakpoint.LogMessage);

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        Assert.Equal("run", resumed.Mode);
        Assert.Null(resumed.LastHit);

        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        var entry = Assert.Single(list.Log ?? []);
        Assert.Equal(set.Breakpoint.Id, entry.BreakpointId);
        Assert.Equal("total=41.80", entry.Message);
        Assert.Equal(1, entry.HitCount);
        Assert.Equal("run", list.Mode);
    }

    [Fact]
    public async Task Logpoint_only_loop_runs_twenty_hits_without_stopping()
    {
        // Seam pin for E2E C6 (loop scenario, 20 TICK hits): a logpoint-only run
        // never stops, buffers exactly the 20 entries in order with hitCount 1-20.
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Scenarios.cs", 19, CancellationToken.None,
            logMessage: "i={i} total={total}");

        for (var i = 0; i < 20; i++)
        {
            var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
            Assert.Equal("run", resumed.Mode);
            Assert.Null(resumed.LastHit);
            if (i < 19)
                await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);
        }

        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal("run", list.Mode);
        Assert.NotNull(list.Log);
        var log = list.Log ?? [];
        Assert.Equal(20, log.Count);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(set.Breakpoint.Id, log[i].BreakpointId);
            Assert.Equal(i + 1, log[i].HitCount);
        }
        Assert.All(log, e => Assert.Equal("i=41.80 total=41.80", e.Message));
    }

    [Theory]
    // run observed: the Go landed.
    [InlineData("run", 1, 5, 1, 5, true)]
    [InlineData("design", 1, 5, 1, 5, true)]
    // Unreadable snapshot: poll loop re-reads rather than settling forever.
    [InlineData("unknown", -1, -1, 1, 5, true)]
    // Another thread's hit: genuinely new, stop settling.
    [InlineData("break", 2, 5, 1, 5, true)]
    // Same thread, count advanced: the resume landed and the tight loop
    // already re-hit (the C6 spurious-stop shape). Settle, let the poll log it.
    [InlineData("break", 1, 6, 1, 5, true)]
    [InlineData("break", 1, 6, 1, 6, false)]
    // Same thread, same count: the Go hasn't landed yet. Keep waiting so the
    // outer poll never buffers the same hit twice.
    [InlineData("break", 1, 5, 1, 5, false)]
    // Same thread, unreadable count: a failed read is not progress. Keep
    // waiting rather than risk a duplicate log entry.
    [InlineData("break", 1, -1, 1, 5, false)]
    // Cleared thread, same count: the Go was accepted but VS still reports the
    // stale break (mode/thread/count are non-atomic reads). Waiting, not
    // another thread: keep waiting for the Go to take effect.
    [InlineData("break", -1, 5, 1, 5, false)]
    // Another readable thread with an unreadable count: genuinely new debugger
    // state, stop settling so the poll loop re-evaluates it.
    [InlineData("break", 2, -1, 1, 5, true)]
    public void Logpoint_settle_exits_only_once_the_resume_took_effect(
        string mode, int settleThread, int settleHits, int loggedThread, int loggedHits, bool expected)
    {
        Assert.Equal(expected,
            DteDebugBridge.LogpointResumeSettled(mode, settleThread, settleHits, loggedThread, loggedHits));
    }

    [Fact]
    public async Task Logpoints_are_isolated_per_session_and_released_on_detach()
    {
        var bridge = new FakeDebugBridge();
        var a = await bridge.AttachAsync("A", CancellationToken.None);
        var b = await bridge.AttachAsync("B", CancellationToken.None);
        await bridge.SetBreakpointAsync(a.SessionId, "A.cs", 1, CancellationToken.None, logMessage: "hit");
        await bridge.ResumeAsync(a.SessionId, 5000, CancellationToken.None);

        var listB = await bridge.ListBreakpointsAsync(b.SessionId, CancellationToken.None);
        Assert.Empty(listB.Breakpoints);
        Assert.Empty(listB.Log ?? []);

        await bridge.DetachAsync(a.SessionId, CancellationToken.None);
        var fresh = await bridge.AttachAsync("A", CancellationToken.None);
        var listFresh = await bridge.ListBreakpointsAsync(fresh.SessionId, CancellationToken.None);
        Assert.Empty(listFresh.Breakpoints);
        Assert.Empty(listFresh.Log ?? []);
    }

    [Fact]
    public async Task False_condition_never_stops_leaving_mode_and_last_hit_untouched()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, condition: "false");

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("run", resumed.Mode);
        Assert.Null(resumed.LastHit);
        var bp = Assert.Single((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
        Assert.Equal("false", bp.Condition);
        Assert.Equal(1, bp.HitCount);
    }

    [Fact]
    public async Task Exact_hit_count_breaks_on_the_Nth_hit_only()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, hitCount: 2);
        Assert.Equal(HitCountModes.Exact, set.Breakpoint.HitCountMode);

        Assert.Equal("run", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);
        Assert.Equal(1, (await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints[0].HitCount);

        await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);
        var second = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        Assert.Equal("break", second.Mode);
        Assert.Equal("Program.cs:42", second.LastHit);

        Assert.Equal("run", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);
    }

    [Fact]
    public async Task Every_hit_count_breaks_on_multiples()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
            hitCount: 2, hitCountMode: HitCountModes.Every);

        Assert.Equal("run", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);
        await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal("break", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);
        Assert.Equal("run", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);
        await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);
        Assert.Equal("break", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);
    }

    [Fact]
    public async Task Condition_and_hit_count_compose()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None,
            condition: "false", hitCount: 1);
        Assert.Equal("run", (await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None)).Mode);

        var (bridge2, sessionId2) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge2, sessionId2, "Program.cs", 42, CancellationToken.None,
            condition: "total > 0", hitCount: 2);
        Assert.Equal("run", (await ExecutionTools.ResumeAsync(bridge2, sessionId2, 5000, CancellationToken.None)).Mode);
        await ExecutionTools.PauseAsync(bridge2, sessionId2, CancellationToken.None);
        var second = await ExecutionTools.ResumeAsync(bridge2, sessionId2, 5000, CancellationToken.None);
        Assert.Equal("break", second.Mode);
        Assert.Equal("Program.cs:42", second.LastHit);
    }

    [Fact]
    public async Task True_condition_stops_like_an_unconditional_breakpoint()
    {
        var (bridge, sessionId) = await AttachedAsync();
        var set = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, condition: "total > 0");

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);

        Assert.Equal("break", resumed.Mode);
        Assert.Equal("Program.cs:42", resumed.LastHit);
    }

    [Fact]
    public async Task Unconditional_breakpoint_keeps_exact_shape_with_empty_options()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var result = await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);

        Assert.Null(result.Breakpoint.Condition);
        Assert.Null(result.Breakpoint.HitCountTarget);
        Assert.Null(result.Breakpoint.HitCountMode);
        Assert.Equal(0, result.Breakpoint.HitCount);
        Assert.Null(result.Breakpoint.LogMessage);
        var list = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);
        Assert.Empty(list.Log ?? []);
    }
}
