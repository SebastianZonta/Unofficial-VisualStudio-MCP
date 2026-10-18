using System.Runtime.InteropServices;
using VsDebugMcp;

// Pure seams: COM error mapping + dispatcher serialization. No VS needed.
public sealed class ComPlumbingTests
{
    [Fact]
    public void Call_rejected_maps_to_vs_busy()
    {
        var ex = ComErrorMapper.ToDomain(new COMException("rejected", unchecked((int)0x80010001)));

        Assert.Equal("vs-busy", ex.Code);
        Assert.Contains("0x80010001", ex.Message);
    }

    [Fact]
    public void Access_denied_maps_to_elevation_mismatch()
    {
        var ex = ComErrorMapper.ToDomain(new COMException("denied", unchecked((int)0x80070005)));

        Assert.Equal("elevation-mismatch", ex.Code);
    }

    [Fact]
    public void Unknown_hresult_maps_to_automation_failed_with_hex()
    {
        var ex = ComErrorMapper.ToDomain(new COMException("boom", unchecked((int)0x80004005)));

        Assert.Equal("automation-failed", ex.Code);
        Assert.Contains("0x80004005", ex.Message);
    }

    [Fact]
    public async Task Dispatcher_runs_work_fifo_on_one_thread()
    {
        using var dispatcher = new ComDispatcher();
        var order = new List<int>();
        var threadIds = new List<int>();

        var tasks = Enumerable.Range(0, 10).Select(i =>
            dispatcher.InvokeAsync(() => { order.Add(i); threadIds.Add(Environment.CurrentManagedThreadId); return i; }, CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(0, 10), results);
        Assert.Equal(Enumerable.Range(0, 10), order);
        Assert.Single(threadIds.Distinct());
    }

    [Fact]
    public void Dispatcher_thread_is_sta()
    {
        using var dispatcher = new ComDispatcher();
        var apartment = dispatcher.InvokeAsync(() => Thread.CurrentThread.GetApartmentState(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(ApartmentState.STA, apartment);
    }
}
