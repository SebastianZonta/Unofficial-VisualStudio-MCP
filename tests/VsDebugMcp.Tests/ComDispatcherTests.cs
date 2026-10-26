using VsDebugMcp;

// ComDispatcher edge contract: timeout, cancellation, error propagation,
// bounded-queue rejection, and disposal. No sleeps, no shared state: every
// test owns its dispatcher and releases any blocked worker before asserting.
public sealed class ComDispatcherTests
{
    [Fact]
    public async Task Blocked_call_times_out_and_dispatcher_stays_usable()
    {
        using var dispatcher = new ComDispatcher();
        using var release = new ManualResetEventSlim(false);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            dispatcher.InvokeAsync(() => { release.Wait(); return 1; }, timeoutMs: 50, CancellationToken.None));
        Assert.Contains("timeoutMs=50", ex.Message);

        release.Set();
        Assert.Equal(42, await dispatcher.InvokeAsync(() => 42, CancellationToken.None));
    }

    [Fact]
    public async Task Queued_call_whose_caller_gave_up_never_runs_and_later_calls_keep_order()
    {
        using var dispatcher = new ComDispatcher();
        using var release = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);
        var ran = new List<string>();

        // The worker parks on the gate; the stale call waits behind it past its budget.
        var gate = dispatcher.InvokeAsync(() => { entered.Set(); release.Wait(); return 0; }, CancellationToken.None);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var stale = dispatcher.InvokeAsync(() => { lock (ran) ran.Add("stale"); return 1; }, timeoutMs: 50, CancellationToken.None);
        await Assert.ThrowsAsync<TimeoutException>(() => stale);

        using var cts = new CancellationTokenSource();
        var cancelled = dispatcher.InvokeAsync(() => { lock (ran) ran.Add("cancelled"); return 2; }, cts.Token);
        var first = dispatcher.InvokeAsync(() => { lock (ran) ran.Add("first"); return 3; }, CancellationToken.None);
        var second = dispatcher.InvokeAsync(() => { lock (ran) ran.Add("second"); return 4; }, CancellationToken.None);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        release.Set();
        Assert.Equal(3, await first);
        Assert.Equal(4, await second);
        Assert.Equal(0, await gate);
        Assert.Equal(["first", "second"], ran);
    }

    [Fact]
    public async Task Pre_canceled_token_throws_and_dispatcher_stays_usable()
    {
        using var dispatcher = new ComDispatcher();
        using var release = new ManualResetEventSlim(false);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Registration runs synchronously inside InvokeAsync while the work
        // is still gated on release, so cancellation always wins: no race.
        var pending = dispatcher.InvokeAsync(() => { release.Wait(); return 1; }, cts.Token);
        release.Set();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(42, await dispatcher.InvokeAsync(() => 42, CancellationToken.None));
    }

    [Fact]
    public async Task Work_exception_propagates_and_dispatcher_stays_usable()
    {
        using var dispatcher = new ComDispatcher();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.InvokeAsync<int>(() => throw new InvalidOperationException("boom"), CancellationToken.None));
        Assert.Equal("boom", ex.Message);

        Assert.Equal(42, await dispatcher.InvokeAsync(() => 42, CancellationToken.None));
    }

    [Fact]
    public async Task Full_queue_rejects_with_vs_busy_then_drains()
    {
        using var dispatcher = new ComDispatcher();
        using var release = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);

        // The single worker parks on the gate; the 100-slot queue fills behind it.
        var gate = dispatcher.InvokeAsync(() => { entered.Set(); release.Wait(); return 0; }, CancellationToken.None);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

        var pending = new List<Task<int>>(100);
        for (var i = 0; i < 100; i++)
        {
            var n = i;
            pending.Add(dispatcher.InvokeAsync(() => n, CancellationToken.None));
        }

        var rejected = await Assert.ThrowsAsync<DebugDomainException>(() =>
            dispatcher.InvokeAsync(() => -1, CancellationToken.None));
        Assert.Equal(ErrorCodes.VsBusy, rejected.Code);

        release.Set();
        Assert.Equal(Enumerable.Range(0, 100), await Task.WhenAll(pending));
        Assert.Equal(0, await gate);
    }

    [Fact]
    public void Filter_instance_rooted()
    {
        var filterType = typeof(ComDispatcher).GetNestedType("OleMessageFilter",
            System.Reflection.BindingFlags.NonPublic)!;
        filterType.GetMethod("Register", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, null);
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var root = filterType.GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null);
            Assert.NotNull(root);
        }
        finally
        {
            filterType.GetMethod("Revoke", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .Invoke(null, null);
        }
    }

    [Fact]
    public async Task Invoke_after_dispose_fails_named_and_dispose_is_idempotent()
    {
        var dispatcher = new ComDispatcher();
        dispatcher.Dispose();
        dispatcher.Dispose();

        var ex = await Assert.ThrowsAsync<DebugDomainException>(() =>
            dispatcher.InvokeAsync(() => 1, CancellationToken.None));
        Assert.Equal(ErrorCodes.VsBusy, ex.Code);
    }
}