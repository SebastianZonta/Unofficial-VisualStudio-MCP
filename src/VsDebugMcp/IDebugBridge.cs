namespace VsDebugMcp;

/// <summary>The one new seam: single boundary between the MCP tools and debugger automation.</summary>
public interface IDebugBridge
{
    Task<AttachResult> AttachAsync(string? instanceOverride, CancellationToken ct);
    Task<ListInstancesResult> ListInstancesAsync(CancellationToken ct);
    Task<ListSessionsResult> ListSessionsAsync(CancellationToken ct);
    Task<DetachResult> DetachAsync(string sessionId, CancellationToken ct);
    Task<ListProcessesResult> ListProcessesAsync(string? nameFilter, bool ownedOnly, CancellationToken ct);
    Task RequireSessionAsync(string sessionId, CancellationToken ct);
    Task<SetBreakpointResult> SetBreakpointAsync(string sessionId, string file, int line, CancellationToken ct,
        string? condition = null, int? hitCount = null, string? hitCountMode = null, string? logMessage = null);
    Task<ListBreakpointsResult> ListBreakpointsAsync(string sessionId, CancellationToken ct);
    Task<ListModulesResult> ListModulesAsync(string sessionId, string? nameFilter, CancellationToken ct);
    Task<RemoveBreakpointResult> RemoveBreakpointAsync(string sessionId, string breakpointId, CancellationToken ct);
    Task<ExecutionResult> PauseAsync(string sessionId, CancellationToken ct);
    Task<ExecutionResult> ResumeAsync(string sessionId, int timeoutMs, CancellationToken ct);
    Task<ExecutionResult> RunToCursorAsync(string sessionId, string file, int line, int timeoutMs, CancellationToken ct);
    Task<ExecutionResult> StepAsync(string sessionId, string direction, int timeoutMs, CancellationToken ct);
    Task<ReadStackResult> ReadStackAsync(string sessionId, int maxFrames, CancellationToken ct);
    Task<ReadVariablesResult> ReadVariablesAsync(string sessionId, string frameId, string? path, int maxVariables, CancellationToken ct);
    Task<EvaluateResult> EvaluateAsync(string sessionId, string expression, int timeoutMs, CancellationToken ct);
    Task<ExecutionResult> AttachProcessAsync(string sessionId, int processId, bool consent, int timeoutMs, CancellationToken ct);
    Task<LaunchProcessResult> LaunchProcessAsync(string sessionId, string executablePath, string? arguments,
        string? workingDirectory, IReadOnlyDictionary<string, string>? environment, int timeoutMs, CancellationToken ct);
}
