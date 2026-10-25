using ModelContextProtocol;
using VsDebugMcp;

// Seam 1 (tool-call boundary): list_modules against fake-seeded module tables.
public sealed class ListModulesTests
{
    private static readonly ModuleInfo[] Table =
    [
        new("Shop.dll", @"C:\app\Shop.dll", Loaded: true, SymbolStates.Loaded, @"C:\app\Shop.pdb"),
        new("Plugin.dll", @"C:\app\Plugin.dll", Loaded: true, SymbolStates.Unknown, ""),
        new("Data.dll", @"C:\app\Data.dll", Loaded: true, SymbolStates.Missing, ""),
        new("Core.dll", @"C:\app\Core.dll", Loaded: true, SymbolStates.Unknown, "module unreadable: 0x80004005"),
    ];

    private static async Task<(FakeDebugBridge Bridge, string SessionId)> RunningAsync(string? instance = null)
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instance, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, attach.SessionId, 5000, CancellationToken.None);
        bridge.SetModules(attach.SessionId, Table);
        return (bridge, attach.SessionId);
    }

    [Fact]
    public async Task Lists_every_module_with_symbol_state_ordered_by_name()
    {
        var (bridge, sessionId) = await RunningAsync();

        var result = await BreakpointTools.ListModulesAsync(bridge, sessionId);

        Assert.Equal("run", result.Mode);
        Assert.Equal(["Core.dll", "Data.dll", "Plugin.dll", "Shop.dll"], result.Modules.Select(m => m.Name));
        var core = result.Modules[0];
        Assert.Equal(SymbolStates.Unknown, core.SymbolState);
        Assert.Equal("module unreadable: 0x80004005", core.SymbolDetail);
        Assert.True(result.Modules[2].Loaded);
        Assert.Equal(@"C:\app\Shop.pdb", result.Modules[3].SymbolDetail);
    }

    [Fact]
    public async Task Name_filter_matches_name_or_path_case_insensitively()
    {
        var (bridge, sessionId) = await RunningAsync();
        bridge.SetModules(sessionId, [.. Table, new("Other.dll", @"C:\shop-bin\Other.dll", true, SymbolStates.Loaded, "x.pdb")]);

        var result = await BreakpointTools.ListModulesAsync(bridge, sessionId, nameFilter: " SHOP ");

        Assert.Equal(["Other.dll", "Shop.dll"], result.Modules.Select(m => m.Name));
        Assert.Equal(5, (await BreakpointTools.ListModulesAsync(bridge, sessionId, nameFilter: "")).Modules.Count);
    }

    [Fact]
    public async Task Same_name_orders_by_path()
    {
        var (bridge, sessionId) = await RunningAsync();
        bridge.SetModules(sessionId,
        [
            new("Shop.dll", @"D:\b\Shop.dll", true, SymbolStates.Loaded, ""),
            new("shop.dll", @"C:\a\Shop.dll", true, SymbolStates.Loaded, ""),
        ]);

        var result = await BreakpointTools.ListModulesAsync(bridge, sessionId);

        Assert.Equal([@"C:\a\Shop.dll", @"D:\b\Shop.dll"], result.Modules.Select(m => m.Path));
    }

    [Fact]
    public async Task Design_mode_fails_not_debugging_instead_of_empty_list()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, null, CancellationToken.None);
        bridge.SetModules(attach.SessionId, Table);

        var ex = await Assert.ThrowsAsync<McpException>(() => BreakpointTools.ListModulesAsync(bridge, attach.SessionId));

        Assert.Equal(ErrorCodes.NotRunning, ex.Data[ErrorCodes.DataKey]);
        Assert.Contains("not debugging", ex.Message);
    }

    [Fact]
    public async Task Break_mode_lists_with_mode_echo()
    {
        var (bridge, sessionId) = await RunningAsync();
        await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);

        var result = await BreakpointTools.ListModulesAsync(bridge, sessionId);

        Assert.Equal("break", result.Mode);
        Assert.Equal(4, result.Modules.Count);
    }

    [Fact]
    public async Task Modules_are_scoped_per_debug_session()
    {
        var bridge = new FakeDebugBridge();
        var a = (await SessionTools.AttachAsync(bridge, "vsA", CancellationToken.None)).SessionId;
        var b = (await SessionTools.AttachAsync(bridge, "vsB", CancellationToken.None)).SessionId;
        await ExecutionTools.ResumeAsync(bridge, a, 5000, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, b, 5000, CancellationToken.None);
        bridge.SetModules(a, Table);
        bridge.SetModules(b, [new("Sibling.dll", @"C:\s\Sibling.dll", true, SymbolStates.Missing, "")]);

        var listA = await BreakpointTools.ListModulesAsync(bridge, a);
        var listB = await BreakpointTools.ListModulesAsync(bridge, b);

        Assert.DoesNotContain(listA.Modules, m => m.Name == "Sibling.dll");
        Assert.Equal("Sibling.dll", Assert.Single(listB.Modules).Name);
    }

    [Fact]
    public async Task Case_only_twins_order_deterministically_whatever_the_input_order()
    {
        var (bridge, sessionId) = await RunningAsync();
        ModuleInfo upper = new("Shop.dll", @"C:\a\Shop.dll", true, SymbolStates.Loaded, "");
        ModuleInfo lower = new("shop.dll", @"C:\a\shop.dll", true, SymbolStates.Loaded, "");

        bridge.SetModules(sessionId, [upper, lower]);
        var first = await BreakpointTools.ListModulesAsync(bridge, sessionId);
        bridge.SetModules(sessionId, [lower, upper]);
        var second = await BreakpointTools.ListModulesAsync(bridge, sessionId);

        Assert.Equal(first.Modules, second.Modules);
    }

    [Fact]
    public async Task Listing_is_read_only()
    {
        var (bridge, sessionId) = await RunningAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);
        var breakpointsBefore = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);

        var first = await BreakpointTools.ListModulesAsync(bridge, sessionId);
        var second = await BreakpointTools.ListModulesAsync(bridge, sessionId);
        var breakpointsAfter = await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None);

        Assert.Equal(first.Modules, second.Modules);
        Assert.Equal(first.Mode, second.Mode);
        Assert.Equal(breakpointsBefore.Breakpoints, breakpointsAfter.Breakpoints);
        Assert.Equal(breakpointsBefore.Mode, breakpointsAfter.Mode);
    }

    [Fact]
    public async Task Empty_session_id_is_invalid_argument()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => BreakpointTools.ListModulesAsync(new FakeDebugBridge(), " "));
        Assert.Contains("invalid-argument", ex.Message);
    }
}
