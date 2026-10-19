using VsDebugMcp;

// Seam: tool boundary + bridge contract for list_processes. No OS calls.
public sealed class ListProcessesTests
{
    private static TargetProcessInfo Proc(int pid, string name, string owner, bool mine)
        => new(pid, name, owner, mine, Error: null);

    [Fact]
    public async Task List_processes_is_sessionless_and_returns_snapshot_entries()
    {
        var bridge = new FakeDebugBridge();
        bridge.Processes.Add(Proc(4321, "API", "FAKE\\dev", mine: true));

        var result = await SessionTools.ListProcessesAsync(bridge, nameFilter: null, ownedOnly: true, CancellationToken.None);

        var single = Assert.Single(result.Processes);
        Assert.Equal(4321, single.Pid);
        Assert.Equal("API", single.Name);
        Assert.True(single.Mine);
    }

    [Fact]
    public async Task Owned_only_default_hides_other_user_processes()
    {
        var bridge = new FakeDebugBridge();
        bridge.Processes.Add(Proc(1, "API", "FAKE\\dev", mine: true));
        bridge.Processes.Add(Proc(2, "svc", "OTHER\\eve", mine: false));

        var result = await SessionTools.ListProcessesAsync(bridge, nameFilter: null, ownedOnly: true, CancellationToken.None);

        var single = Assert.Single(result.Processes);
        Assert.Equal(1, single.Pid);
    }

    [Fact]
    public async Task Name_filter_selects_the_program_under_test()
    {
        var bridge = new FakeDebugBridge();
        bridge.Processes.Add(Proc(1, "API", "FAKE\\dev", mine: true));
        bridge.Processes.Add(Proc(2, "svc-host", "FAKE\\dev", mine: true));

        var result = await SessionTools.ListProcessesAsync(bridge, "api", ownedOnly: true, CancellationToken.None);

        var single = Assert.Single(result.Processes);
        Assert.Equal(1, single.Pid);
    }

    [Fact]
    public async Task Listed_pid_attaches_with_consent()
    {
        var bridge = new FakeDebugBridge();
        bridge.Processes.Add(Proc(4321, "API", "FAKE\\dev", mine: true));
        bridge.ProcessOwners[4321] = "FAKE\\dev";
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);

        var listed = await SessionTools.ListProcessesAsync(bridge, "api", ownedOnly: true, CancellationToken.None);
        var pid = Assert.Single(listed.Processes).Pid;
        var result = await SessionTools.AttachProcessAsync(bridge, attach.SessionId, pid, consent: true, 30_000, CancellationToken.None);

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task Flagged_entry_does_not_fail_the_list()
    {
        var bridge = new FakeDebugBridge();
        bridge.Processes.Add(Proc(1, "API", "FAKE\\dev", mine: true));
        bridge.Processes.Add(new TargetProcessInfo(2, "locked", "", Mine: false, Error: "owner lookup denied"));

        var result = await SessionTools.ListProcessesAsync(bridge, nameFilter: null, ownedOnly: false, CancellationToken.None);

        Assert.Equal(2, result.Processes.Count);
        var flagged = result.Processes.Single(p => p.Pid == 2);
        Assert.NotNull(flagged.Error);
    }
}
