using ModelContextProtocol;
using VsDebugMcp;

// One read-only rule (spec .scratch/audit-must-fix) for every expression the agent
// sends: evaluate, read_variables path, breakpoint conditions, logpoint holes.
public sealed class ReadOnlyGateTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> BrokenAsync()
    {
        var bridge = new FakeDebugBridge();
        var sessionId = (await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None)).SessionId;
        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);
        await ExecutionTools.ResumeAsync(bridge, sessionId, 5000, CancellationToken.None);
        return (bridge, sessionId);
    }

    [Theory]
    [InlineData("counter++")]
    [InlineData("++counter")]
    [InlineData("counter--")]
    [InlineData("--counter")]
    [InlineData("a <<= 1")]
    [InlineData("a >>= 1")]
    [InlineData("total = 0")]
    public async Task Evaluate_rejects_mutating_expressions(string expression)
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, expression, 5000, CancellationToken.None));
        Assert.Contains("invalid-argument", ex.Message);
    }

    [Theory]
    [InlineData("a == b")]
    [InlineData("a != b")]
    [InlineData("a <= b")]
    [InlineData("a >= b")]
    [InlineData("a << 1 > b >> 2")]
    [InlineData("a > (b)")]
    [InlineData("x => x")]
    [InlineData("-total")]
    [InlineData("a - -b")]
    [InlineData("i == 3 || i == 4")]
    [InlineData("(i == 3)")]
    [InlineData("order.Items.Count")]
    [InlineData("order.Items[0].Sku")]
    [InlineData("order.Coupon == null")]
    [InlineData("counter.NextTicket")]
    public void Read_only_gate_still_accepts_comparisons_reads_and_indexers(string expression)
    {
        // Gate-level acceptance only: whether an unknown name evaluates is the
        // debugger's call (live invalid-expression), never the fake's canned value.
        Assert.Null(BreakpointOptions.CheckExpression(expression));
    }

    [Theory]
    [InlineData("counter.Increment()")]
    [InlineData("foo<T>(x)")]
    [InlineData("order.Items.Clear()")]
    [InlineData("items.Remove(1)")]
    [InlineData("order.ComputeSubtotal()")]
    [InlineData("order.Items.Count(x => x.Qty == 5)")]
    [InlineData("items.Where(x => x.Id > 0).Count()")]
    public async Task Evaluate_rejects_invocations_as_invalid_expression(string expression)
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, expression, 5000, CancellationToken.None));
        Assert.Contains("invalid-expression", ex.Message);
    }

    [Fact]
    public async Task Mixed_and_null_expressions_keep_the_first_gate_code()
    {
        var (bridge, sessionId) = await BrokenAsync();

        var mixed = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, "a; foo()", 5000, CancellationToken.None));
        Assert.Contains("invalid-argument", mixed.Message);

        var nil = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.EvaluateAsync(bridge, sessionId, null!, 5000, CancellationToken.None));
        Assert.Contains("invalid-argument", nil.Message);
    }

    [Theory]
    [InlineData("items = null")]
    [InlineData("items; total")]
    [InlineData("items\ntotal")]
    [InlineData("total++")]
    [InlineData("--total")]
    [InlineData("total <<= 1")]
    [InlineData("total >>= 1")]
    [InlineData(" ")]
    public async Task Read_variables_path_gets_the_same_read_only_rule(string path)
    {
        var (bridge, sessionId) = await BrokenAsync();

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", path, 50, CancellationToken.None));
        Assert.Contains("invalid-argument", ex.Message);
        Assert.Contains("path", ex.Message);
    }

    [Fact]
    public async Task Incrementing_condition_fails_with_nothing_created()
    {
        var bridge = new FakeDebugBridge();
        var sessionId = (await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None)).SessionId;

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, condition: "n++ > 0"));
        Assert.Contains("invalid-argument", ex.Message);
        Assert.Contains("condition", ex.Message);

        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }

    [Fact]
    public async Task Incrementing_logpoint_hole_fails_with_nothing_created()
    {
        var bridge = new FakeDebugBridge();
        var sessionId = (await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None)).SessionId;

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None, logMessage: "i={i++}"));
        Assert.Contains("invalid-argument", ex.Message);
        Assert.Contains("logMessage", ex.Message);

        Assert.Empty((await BreakpointTools.ListBreakpointsAsync(bridge, sessionId, CancellationToken.None)).Breakpoints);
    }
}
