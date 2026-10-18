using System.ComponentModel;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class SessionTools
{
    [McpServerTool(Name = "attach")]
    [Description("Attach to the running VS 2026 instance and open a debug session. Returns a sessionId scoping all later calls.")]
    public static Task<AttachResult> AttachAsync(
        IDebugBridge bridge,
        [Description("Optional explicit VS instance override. Omit to bind the newest running VS 2026.")]
        string? instanceOverride,
        CancellationToken cancellationToken)
        => bridge.AttachAsync(instanceOverride, cancellationToken);

    [McpServerTool(Name = "attach_process")]
    [Description("Attach the debugger to a local process by ID. Requires explicit consent:true and same-user; solution launch via attach stays frictionless.")]
    public static Task<ExecutionResult> AttachProcessAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Local process ID to attach to.")] int processId,
        [Description("Explicit consent to attach to an arbitrary process. Must be true.")] bool consent = false,
        [Description("Budget in milliseconds for the attach (default 30000).")] int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
        => ToolErrors.MapAsync(() => bridge.AttachProcessAsync(sessionId, processId, consent, timeoutMs, cancellationToken));
}
