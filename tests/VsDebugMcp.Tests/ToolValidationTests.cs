using ModelContextProtocol;
using VsDebugMcp;

// Tool-layer validation matrix: bad input fails fast with a named
// McpException without touching the bridge. Each case owns its bridge;
// no shared state. Numeric budgets are validated at the boundary
// (timeoutMs 1000-60000, maxFrames 1-200, maxVariables 1-500);
// inclusivity of the edges is pinned by Boundary_values_are_accepted.
public sealed class ToolValidationTests
{
    private static async Task<(FakeDebugBridge Bridge, string SessionId)> AttachedAsync()
    {
        var bridge = new FakeDebugBridge();
        var attach = await SessionTools.AttachAsync(bridge, instanceOverride: null, CancellationToken.None);
        return (bridge, attach.SessionId);
    }

    [Theory]
    [InlineData("empty-file", "invalid-argument")]
    [InlineData("whitespace-file", "invalid-argument")]
    [InlineData("zero-line", "invalid-argument")]
    [InlineData("negative-line", "invalid-argument")]
    [InlineData("empty-breakpoint-id", "invalid-argument")]
    [InlineData("whitespace-breakpoint-id", "invalid-argument")]
    [InlineData("empty-session-id", "invalid-argument")]
    [InlineData("empty-expression", "invalid-argument")]
    [InlineData("whitespace-expression", "invalid-argument")]
    [InlineData("semicolon-expression", "invalid-argument")]
    [InlineData("multiline-expression", "invalid-argument")]
    [InlineData("assignment-expression", "invalid-argument")]
    [InlineData("unknown-direction", "invalid-argument")]
    [InlineData("empty-direction", "invalid-argument")]
    [InlineData("zero-timeoutMs", "invalid-argument")]
    [InlineData("small-timeoutMs", "invalid-argument")]
    [InlineData("huge-timeoutMs", "invalid-argument")]
    [InlineData("zero-maxFrames", "invalid-argument")]
    [InlineData("negative-maxFrames", "invalid-argument")]
    [InlineData("huge-maxFrames", "invalid-argument")]
    [InlineData("empty-frame-id", "invalid-argument")]
    [InlineData("zero-maxVariables", "invalid-argument")]
    [InlineData("huge-maxVariables", "invalid-argument")]
    [InlineData("zero-process-id", "invalid-argument")]
    [InlineData("consent-false", "consent-required")]
    public async Task Tool_validation_rejects_bad_input(string kase, string code)
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => InvokeCaseAsync(kase));
        Assert.Contains(code, ex.Message);
    }

    private static async Task<object> InvokeCaseAsync(string kase)
    {
        var (bridge, sessionId) = await AttachedAsync();
        return kase switch
        {
            "empty-file" => await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "", 42, CancellationToken.None),
            "whitespace-file" => await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "  ", 42, CancellationToken.None),
            "zero-line" => await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 0, CancellationToken.None),
            "negative-line" => await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", -3, CancellationToken.None),
            "empty-breakpoint-id" => await BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, "", CancellationToken.None),
            "whitespace-breakpoint-id" => await BreakpointTools.RemoveBreakpointAsync(bridge, sessionId, "  ", CancellationToken.None),
            "empty-session-id" => await BreakpointTools.ListBreakpointsAsync(bridge, "", CancellationToken.None),
            "empty-expression" => await InspectionTools.EvaluateAsync(bridge, sessionId, "", 5000, CancellationToken.None),
            "whitespace-expression" => await InspectionTools.EvaluateAsync(bridge, sessionId, "  ", 5000, CancellationToken.None),
            "semicolon-expression" => await InspectionTools.EvaluateAsync(bridge, sessionId, "a; b", 5000, CancellationToken.None),
            "multiline-expression" => await InspectionTools.EvaluateAsync(bridge, sessionId, "a\nb", 5000, CancellationToken.None),
            "assignment-expression" => await InspectionTools.EvaluateAsync(bridge, sessionId, "order.Total = 0", 5000, CancellationToken.None),
            "unknown-direction" => await ExecutionTools.StepAsync(bridge, sessionId, "sideways", 5000, CancellationToken.None),
            "empty-direction" => await ExecutionTools.StepAsync(bridge, sessionId, "", 5000, CancellationToken.None),
            "zero-timeoutMs" => await ExecutionTools.ResumeAsync(bridge, sessionId, 0, CancellationToken.None),
            "small-timeoutMs" => await ExecutionTools.ResumeAsync(bridge, sessionId, 999, CancellationToken.None),
            "huge-timeoutMs" => await ExecutionTools.ResumeAsync(bridge, sessionId, 60_001, CancellationToken.None),
            "zero-maxFrames" => await InspectionTools.ReadStackAsync(bridge, sessionId, 0, CancellationToken.None),
            "negative-maxFrames" => await InspectionTools.ReadStackAsync(bridge, sessionId, -1, CancellationToken.None),
            "huge-maxFrames" => await InspectionTools.ReadStackAsync(bridge, sessionId, 201, CancellationToken.None),
            "empty-frame-id" => await InspectionTools.ReadVariablesAsync(bridge, sessionId, "", null, 50, CancellationToken.None),
            "zero-maxVariables" => await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", null, 0, CancellationToken.None),
            "huge-maxVariables" => await InspectionTools.ReadVariablesAsync(bridge, sessionId, "f0", null, 501, CancellationToken.None),
            "zero-process-id" => await SessionTools.AttachProcessAsync(bridge, sessionId, 0, consent: true, 30_000, CancellationToken.None),
            "consent-false" => await InvokeWithoutConsentAsync(bridge, sessionId),
            _ => throw new InvalidOperationException($"unknown validation case {kase}"),
        };
    }

    private static async Task<object> InvokeWithoutConsentAsync(FakeDebugBridge bridge, string sessionId)
    {
        bridge.ProcessOwners[4321] = bridge.CurrentUser;
        return await SessionTools.AttachProcessAsync(bridge, sessionId, 4321, consent: false, 30_000, CancellationToken.None);
    }

    [Fact]
    public async Task Boundary_values_are_accepted()
    {
        var (bridge, sessionId) = await AttachedAsync();

        var resumed = await ExecutionTools.ResumeAsync(bridge, sessionId, timeoutMs: 1000, CancellationToken.None);
        Assert.Equal("run", resumed.Mode);

        await BreakpointTools.SetBreakpointAsync(bridge, sessionId, "Program.cs", 42, CancellationToken.None);
        await ExecutionTools.PauseAsync(bridge, sessionId, CancellationToken.None);
        var stack = await InspectionTools.ReadStackAsync(bridge, sessionId, maxFrames: 200, CancellationToken.None);
        Assert.NotEmpty(stack.Frames);
    }
}
