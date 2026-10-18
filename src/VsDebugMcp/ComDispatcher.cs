using System.Collections.Concurrent;

namespace VsDebugMcp;

/// <summary>One STA dispatcher thread serializing all EnvDTE COM calls. Parallel agent requests queue; never concurrent COM.</summary>
internal sealed class ComDispatcher : IDisposable
{
    private readonly BlockingCollection<(Func<object?> Work, TaskCompletionSource<object?> Done)> _queue = new();
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
        _queue.Add((() => work(), done));
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
        _queue.CompleteAdding();
    }
}
