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

    [McpServerTool(Name = "list_instances")]
    [Description("List running VS 2026 instances with solution and project names. Session-less and read-only; attach with the entry's display name.")]
    public static Task<ListInstancesResult> ListInstancesAsync(
        IDebugBridge bridge,
        CancellationToken cancellationToken)
        => ToolErrors.MapAsync(() => bridge.ListInstancesAsync(cancellationToken));

    [McpServerTool(Name = "list_processes")]
    [Description("List local target processes (program under test candidates). Session-less and read-only; attach with the entry's pid plus consent:true.")]
    public static Task<ListProcessesResult> ListProcessesAsync(
        IDebugBridge bridge,
        [Description("Optional case-insensitive substring filter on process name.")] string? nameFilter = null,
        [Description("When true (default), only processes owned by the bridge user are listed.")] bool ownedOnly = true,
        CancellationToken cancellationToken = default)
        => ToolErrors.MapAsync(() => bridge.ListProcessesAsync(nameFilter, ownedOnly, cancellationToken));

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
