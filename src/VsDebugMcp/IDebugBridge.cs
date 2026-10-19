namespace VsDebugMcp;

/// <summary>The one new seam: single boundary between the MCP tools and debugger automation.</summary>
public interface IDebugBridge
{
    Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken ct);
    Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct);
    Task RequireSessionAsync(string sessionId, CancellationToken ct);
    Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct);
    Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct);
    Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct);
    Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct);
    Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct);
    Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct);
    Task<ReadStackResult> ReadStackAsync(string sessionId, int maxFrames, CancellationToken ct);
    Task<ReadVariablesResult> ReadVariablesAsync(string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct);
    Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct);
    Task<ExecutionResult> AttachProcessAsync(string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct);
}
