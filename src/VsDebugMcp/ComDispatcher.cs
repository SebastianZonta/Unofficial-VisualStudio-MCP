using System.Collections.Concurrent;

namespace VsDebugMcp;

/// <summary>One STA dispatcher thread serializing all EnvDTE COM calls. Parallel agent requests queue; never concurrent COM.</summary>
internal sealed class ComDispatcher : IDisposable
{
    private const int BoundedCapacity = 100;
    private readonly BlockingCollection<(Func<object?> Work, TaskCompletionSource<object?> Done)> _queue =
        new(BoundedCapacity);
    private readonly Thread _thread;
    private bool _disposed;

    public ComDispatcher()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "dte-dispatcher" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public async Task<T> InvokeAsync<T>(Func<T> work, CancellationToken ct)
    {
        var done = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued;
        try
        {
            enqueued = !_disposed && !_queue.IsAddingCompleted && _queue.TryAdd((() => work(), done));
        }
        catch (ObjectDisposedException)
        {
            enqueued = false;
        }
        if (!enqueued)
        {
            throw new DebugDomainException(
                "vs-busy",
                _disposed
                    ? "debugger dispatcher is shutting down"
                    : "debugger dispatcher queue is full (100 pending calls)",
                "dismiss any modal dialog in VS and retry");
        }
        using var reg = ct.Register(() => done.TrySetCanceled(ct));
        return (T)(await done.Task.ConfigureAwait(false))!;
    }

    public async Task<T> InvokeAsync<T>(Func<T> work, int timeoutMs, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            return await InvokeAsync(work, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"DTE call exceeded timeoutMs={timeoutMs}");
        }
    }

    private void Run()
    {
        foreach (var (work, done) in _queue.GetConsumingEnumerable())
        {
            // The caller already gave up (timeout/cancel): never issue stale work late,
            // so a reported timeout means nothing ran behind the caller's back.
            if (done.Task.IsCompleted)
                continue;
            try
            {
                done.TrySetResult(work());
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _queue.CompleteAdding(); } catch { /* best effort */ }
        // Never park process exit on a wedged STA thread: bounded join, then drop it (IsBackground).
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}
