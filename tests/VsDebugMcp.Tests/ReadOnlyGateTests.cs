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
    [InlineData("items.Where(x => x.Id > 0).Count()")]
    [InlineData("-total")]
    [InlineData("a - -b")]
    public async Task Evaluate_still_accepts_comparisons_and_lambdas(string expression)
    {
        var (bridge, sessionId) = await BrokenAsync();

        var eval = await InspectionTools.EvaluateAsync(bridge, sessionId, expression, 5000, CancellationToken.None);
        Assert.Equal(expression, eval.Expression);
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
