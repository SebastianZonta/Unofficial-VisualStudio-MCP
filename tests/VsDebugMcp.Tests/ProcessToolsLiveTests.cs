using VsDebugMcp;

// Live-bridge regression for e2e-2026-10-23 issue 01: list_processes and the
// attach_process success path threw bare host errors (no mapped code) because
// identity/WMI throws escaped the shared ProcessOwner path. list_processes is
// session-less so the live bridge runs here with no VS instance; attach_process
// needs a bound VS, so the attach side is pinned via the shared fail-closed
// owner contract instead. Windows-only, like the existing OwnerOf test.
public sealed class ProcessToolsLiveTests
{
    private static void SkipUnlessWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw Xunit.Sdk.SkipException.ForSkip("Process enumeration and WMI owner lookup are Windows-only.");
    }

    [Fact]
    public async Task Live_list_processes_returns_entries_for_every_e2e_arg_shape()
    {
        SkipUnlessWindows();
        using var bridge = new DteDebugBridge();

        // The four arg combos from E2E rows D3/D4, all of which threw bare host errors.
        foreach (var (nameFilter, ownedOnly) in new (string?, bool)[] { (null, true), (null, false), ("sandbox", true), ("sandbox", false) })
        {
            var result = await SessionTools.ListProcessesAsync(bridge, nameFilter, ownedOnly, CancellationToken.None);
            Assert.NotNull(result.Processes);
        }

        var unfiltered = await SessionTools.ListProcessesAsync(bridge, null, ownedOnly: false, CancellationToken.None);
        var self = unfiltered.Processes.Single(p => p.Pid == Environment.ProcessId);
        Assert.Equal(ProcessOwner.CurrentUserName(), self.Owner);
        Assert.True(self.Mine);

        var owned = await SessionTools.ListProcessesAsync(bridge, null, ownedOnly: true, CancellationToken.None);
        Assert.Contains(owned.Processes, p => p.Pid == Environment.ProcessId);
    }

    [Fact]
    public void Owner_lookup_fails_closed_for_unknown_pids()
    {
        SkipUnlessWindows();

        // Fail closed (never a bare throw): unknown pids read null, which the
        // attach path maps to user-mismatch downstream.
        Assert.Null(ProcessOwner.OwnerOf(999_999_999));
        Assert.NotNull(ProcessOwner.SnapshotOwners());
    }
}
