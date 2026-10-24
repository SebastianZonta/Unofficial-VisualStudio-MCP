using ModelContextProtocol;
using VsDebugMcp;

// Seam 1 (tool-call boundary): drive InspectionTools against the fake bridge.
// Seam 2 (bridge interface): canned frames/variables + break-mode gating underneath.
public sealed class InspectionTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    private static async Task<(FakeDebugBridge Bridge, string SessionId)> BrokenAsync()
    {
        var (bridge, sessionId) = await AttachedAsync();
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        return (bridge, sessionId);
    }

    [Fact]
    public async Task Read_stack_returns_capped_frames_with_mode()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var stack = await InspectionTools.ReadStackAsync(bridge, sessionId, 20, CancellationToken.None);

        Assert.Equal("break", stack.Mode);
        Assert.Equal(2, stack.Frames.Count);
        Assert.Equal("f0", stack.Frames[0].Id);
        Assert.Contains("Total", stack.Frames[0].Function);

        var capped = await InspectionTools.ReadStackAsync(bridge, sessionId, 1, CancellationToken.None);
        Assert.Single(capped.Frames);
    }

    [Fact]
    public async Task Read_variables_returns_truncated_values_with_expandable()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var vars = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", null, 50, CancellationToken.None);

        Assert.Equal("break", vars.Mode);
        var items = Assert.Single(vars.Variables, v => v.Name == "items");
        Assert.True(items.Expandable);
        Assert.Contains("truncated", items.Value);
        Assert.False(string.IsNullOrWhiteSpace(items.Path));
    }

    [Fact]
    public async Task Drill_down_by_path_returns_children()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var children = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", "items", 50, CancellationToken.None);

        Assert.Equal("items", children.Path);
        Assert.NotEmpty(children.Variables);

        var badPath = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", "nope", 50, CancellationToken.None));
        Assert.Contains("path-not-found", badPath.Message);
    }

    [Fact]
    public async Task Each_frame_reads_its_own_variables_top_level_and_drill_down()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var f0 = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", null, 50, CancellationToken.None);
        var f1 = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f1", null, 50, CancellationToken.None);
        Assert.Equal("41.80", Assert.Single(f0.Variables, v => v.Name == "total").Value);
        Assert.Equal("99.00", Assert.Single(f1.Variables, v => v.Name == "total").Value);

        var drilled = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f1", "items", 50, CancellationToken.None);
        Assert.Equal("f1", drilled.FrameId);
        Assert.Equal("3", Assert.Single(drilled.Variables, v => v.Name == "items.Count").Value);
    }

    [Fact]
    public async Task Evaluate_runs_in_the_top_frame_after_reading_another_frame()
    {
        var (bridge, sessionId) = await BrokenAsync();

        await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f1", null, 50, CancellationToken.None);
        await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f1", "items", 50, CancellationToken.None);
        var eval = await InspectionTools.EvaluateAsync(bridge, sessionId, "total", 5000, CancellationToken.None);

        Assert.Equal("41.80", eval.Value);
    }

    [Fact]
    public async Task Unknown_frame_with_path_fails_frame_not_found()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadVariablesAsync(bridge, sessionId, "f99", "items", 50, CancellationToken.None));
        Assert.Contains("frame-not-found", ex.Message);
    }

    [Fact]
    public async Task Unknown_frame_fails_named()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadVariablesAsync(bridge, sessionId, "f99", null, 50, CancellationToken.None));
        Assert.Contains("frame-not-found", ex.Message);
    }

    [Fact]
    public async Task Evaluate_is_read_only_value_with_type()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var eval = await InspectionTools.EvaluateAsync(bridge, sessionId, "total", 5000, CancellationToken.None);

        Assert.Equal("total", eval.Expression);
        Assert.False(string.IsNullOrWhiteSpace(eval.Value));
        Assert.False(string.IsNullOrWhiteSpace(eval.Type));
        Assert.Equal("break", eval.Mode);
    }

    [Fact]
    public async Task Evaluate_rejects_empty_and_statement_expressions()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var empty = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, " ", 5000, CancellationToken.None));
        Assert.Contains("invalid-argument", empty.Message);

        var statement = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "order.Total = 0;", 5000, CancellationToken.None));
        Assert.Contains("invalid-argument", statement.Message);
    }

    [Fact]
    public async Task Evaluate_timeout_and_invalid_expression_are_named()
    {
        var (bridge, sessionId) = await BrokenAsync();

        bridge.ForceTimeout = true;
        var timeout = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "order.Total", 5000, CancellationToken.None));
        Assert.Contains("timeout", timeout.Message);

        bridge.ForceTimeout = false;
        bridge.ForceInvalidExpression = true;
        var invalid = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "order.Nope", 5000, CancellationToken.None));
        Assert.Contains("invalid-expression", invalid.Message);
    }

    [Fact]
    public async Task Inspect_outside_break_mode_fails_named()
    {
        var (bridge, sessionId) = await AttachedAsync(); // design mode

        var stack = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadStackAsync(bridge, sessionId, 20, CancellationToken.None));
        Assert.Contains("not-in-break-mode", stack.Message);

        var vars = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", null, 50, CancellationToken.None));
        Assert.Contains("not-in-break-mode", vars.Message);

        var eval = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "order.Total", 5000, CancellationToken.None));
        Assert.Contains("not-in-break-mode", eval.Message);
    }

    [Fact]
    public async Task TopLevel_expandable_has_path()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var vars = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", null, 50, CancellationToken.None);

        var order = Assert.Single(vars.Variables, v => v.Name == "order");
        Assert.True(order.Expandable);
        Assert.Equal("order", order.Path);

        var children = await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", order.Path, 50, CancellationToken.None);
        Assert.Equal("order", children.Path);
        Assert.Contains(children.Variables, v => v.Name == "order.Id");
    }

    [Fact]
    public async Task Evaluate_unknown_fails_like_live()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "order.Nope", 5000, CancellationToken.None));
        Assert.Contains("invalid-expression", ex.Message);
    }

    [Fact]
    public async Task Unknown_session_fails_named_no_session()
    {
        var bridge = new FakeDebugBridge();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadStackAsync(bridge, "bogus-session-id", 20, CancellationToken.None));
        Assert.Contains("no-session", ex.Message);
    }
}
