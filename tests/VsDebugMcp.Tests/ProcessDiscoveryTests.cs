using VsDebugMcp;

// Pure seam: target-process name filtering, owned-only filtering, ordering. No OS calls.
public sealed class ProcessDiscoveryTests
{
    private static TargetProcessInfo Proc(int pid, string name, bool mine, string? error = null)
        => new(pid, name, mine ? "FAKE\\dev" : "OTHER\\eve", mine, error);

    [Fact]
    public void Empty_input_lists_nothing()
    {
        Assert.Empty(ProcessDiscovery.FilterAndOrder([], nameFilter: null, ownedOnly: true));
    }

    [Fact]
    public void Owned_only_excludes_other_user_entries()
    {
        var input = new[]
        {
            Proc(1, "api", mine: true),
            Proc(2, "svc", mine: false),
        };

        var result = ProcessDiscovery.FilterAndOrder(input, nameFilter: null, ownedOnly: true);

        var single = Assert.Single(result);
        Assert.Equal(1, single.Pid);
    }

    [Fact]
    public void Owned_only_off_keeps_other_user_entries()
    {
        var input = new[]
        {
            Proc(1, "api", mine: true),
            Proc(2, "svc", mine: false),
        };

        var result = ProcessDiscovery.FilterAndOrder(input, nameFilter: null, ownedOnly: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Owned_only_excludes_unknown_owner_but_keeps_mine_flagged_entries()
    {
        var input = new[]
        {
            Proc(1, "api", mine: true, error: "owner unknown: WMI lookup failed; failing closed"),
            Proc(2, "svc", mine: false),
        };

        var result = ProcessDiscovery.FilterAndOrder(input, nameFilter: null, ownedOnly: true);

        var single = Assert.Single(result);
        Assert.Equal(1, single.Pid);
        Assert.NotNull(single.Error);
    }

    [Fact]
    public void Name_filter_matches_case_insensitively()
    {
        var input = new[]
        {
            Proc(10, "API", mine: true),
            Proc(20, "svc-host", mine: true),
        };

        var result = ProcessDiscovery.FilterAndOrder(input, nameFilter: "api", ownedOnly: true);

        var single = Assert.Single(result);
        Assert.Equal(10, single.Pid);
    }

    [Fact]
    public void Empty_name_filter_means_no_name_filtering()
    {
        var input = new[]
        {
            Proc(10, "API", mine: true),
            Proc(20, "svc", mine: true),
        };

        Assert.Equal(2, ProcessDiscovery.FilterAndOrder(input, nameFilter: null, ownedOnly: true).Count);
        Assert.Equal(2, ProcessDiscovery.FilterAndOrder(input, nameFilter: "", ownedOnly: true).Count);
        Assert.Equal(2, ProcessDiscovery.FilterAndOrder(input, nameFilter: "  ", ownedOnly: true).Count);
    }

    [Fact]
    public void Ordering_is_by_name_then_pid()
    {
        var input = new[]
        {
            Proc(30, "svc", mine: true),
            Proc(10, "api", mine: true),
            Proc(20, "api", mine: true),
        };

        var result = ProcessDiscovery.FilterAndOrder(input, nameFilter: null, ownedOnly: true);

        Assert.Equal([10, 20, 30], result.Select(p => p.Pid));
    }
}
