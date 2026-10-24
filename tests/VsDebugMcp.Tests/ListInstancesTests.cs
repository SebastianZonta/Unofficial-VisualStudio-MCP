using VsDebugMcp;

// Seam: tool boundary + bridge contract for list_instances. No COM.
public sealed class ListInstancesTests
{
    [Fact]
    public async Task List_instances_is_sessionless_and_returns_snapshot_entries()
    {
        var bridge = new FakeDebugBridge();
        bridge.Instances.Add(new VsInstanceInfo(
            "!VisualStudio.DTE.18.0:222", 222, "18.0",
            "Shop", @"C:\src\Shop.sln",
            ["Shop.Api", "Shop.Core"], true, null));

        var result = await SessionTools.ListInstancesAsync(bridge, CancellationToken.None);

        var single = Assert.Single(result.Instances);
        Assert.Equal("!VisualStudio.DTE.18.0:222 [fake]", single.DisplayName);
        Assert.Equal(222, single.Pid);
        Assert.Equal("Shop", single.SolutionName);
        Assert.Contains("Shop.Api", single.ProjectNames);
    }

    [Fact]
    public async Task Empty_solution_is_data_not_an_error()
    {
        var bridge = new FakeDebugBridge();
        bridge.Instances.Add(new VsInstanceInfo(
            "!VisualStudio.DTE.18.0:444", 444, "18.0",
            "", "", [], true, null));

        var result = await SessionTools.ListInstancesAsync(bridge, CancellationToken.None);

        var single = Assert.Single(result.Instances);
        Assert.True(single.Reachable);
        Assert.Null(single.Error);
        Assert.Empty(single.ProjectNames);
    }

    [Fact]
    public async Task Unreachable_instance_is_flagged_without_failing_the_list()
    {
        var bridge = new FakeDebugBridge();
        bridge.Instances.Add(new VsInstanceInfo(
            "!VisualStudio.DTE.18.0:222", 222, "18.0",
            "Shop", @"C:\src\Shop.sln", ["Shop.Api"], true, null));
        bridge.Instances.Add(new VsInstanceInfo(
            "!VisualStudio.DTE.18.0:555", 555, "18.0",
            "", "", [], false, "vs-busy: modal dialog"));

        var result = await SessionTools.ListInstancesAsync(bridge, CancellationToken.None);

        Assert.Equal(2, result.Instances.Count);
        var flagged = result.Instances.Single(i => i.Pid == 555);
        Assert.False(flagged.Reachable);
        Assert.NotNull(flagged.Error);
    }

    [Fact]
    public async Task Fake_bridge_marks_display_names_so_canned_values_are_unmistakable()
    {
        var bridge = new FakeDebugBridge();
        bridge.Instances.Add(new VsInstanceInfo(
            "!VisualStudio.DTE.18.0:222", 222, "18.0",
            "Shop", @"C:\src\Shop.sln", ["Shop.Api"], true, null));

        var instances = await SessionTools.ListInstancesAsync(bridge, CancellationToken.None);
        Assert.EndsWith(" [fake]", Assert.Single(instances.Instances).DisplayName);

        var attach = await SessionTools.AttachAsync(bridge, null, CancellationToken.None);
        var sessions = await SessionTools.ListSessionsAsync(bridge, CancellationToken.None);
        Assert.EndsWith(" [fake]", Assert.Single(sessions.Sessions).DisplayName);
        Assert.Equal(attach.SessionId, Assert.Single(sessions.Sessions).SessionId);
    }
}
