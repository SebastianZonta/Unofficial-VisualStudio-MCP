using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace VsDebugMcp;

/// <summary>One STA dispatcher thread serializing all EnvDTE COM calls. Parallel agent requests queue; never concurrent COM.
/// The thread registers an OLE message filter that retries calls a busy VS rejects
/// for a bounded window before surfacing vs-busy, so agents need no hand-rolled retry loop.</summary>
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
        // Retry calls a busy VS rejects (modal dialog, blocked UI thread) for a
        // bounded window inside COM; after that the existing vs-busy mapping applies.
        // Compatible with per-session lanes: every lane thread registers its own filter.
        OleMessageFilter.Register();
        try
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
        finally
        {
            OleMessageFilter.Revoke();
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

    /// <summary>OLE message filter: retries server-busy rejections briefly on the calling
    /// STA thread. dwTickCount is elapsed ms since the outgoing call was made, so the
    /// window counts against the caller's own budget and never stretches past it by
    /// more than one retry interval; afterwards COM returns RPC_E_CALL_REJECTED and
    /// the existing vs-busy mapping applies.</summary>
    private sealed class OleMessageFilter : IOleMessageFilter
    {
        private const int RetryWindowMs = 3_000;
        private const int RetryAfterMs = 100;

        private OleMessageFilter() { }

        public static void Register()
        {
            try { _ = NativeMethods.CoRegisterMessageFilter(new OleMessageFilter(), out _); }
            catch { /* best effort: without a filter every busy call just surfaces vs-busy */ }
        }

        public static void Revoke()
        {
            try { _ = NativeMethods.CoRegisterMessageFilter(null, out _); }
            catch { /* best effort */ }
        }

        public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) =>
            NativeMethods.SERVERCALL_ISHANDLED;

        public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) =>
            dwRejectType == NativeMethods.SERVERCALL_REJECTED && dwTickCount < RetryWindowMs
                ? RetryAfterMs
                : -1;

        public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) =>
            NativeMethods.PENDINGMSG_WAITDEFPROCESS;

        private static class NativeMethods
        {
            internal const int SERVERCALL_ISHANDLED = 0;
            internal const int SERVERCALL_REJECTED = 1;
            internal const int PENDINGMSG_WAITDEFPROCESS = 2;

            [DllImport("ole32.dll")]
            internal static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
        }
    }

    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleMessageFilter
    {
        [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
        [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
        [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }
}
