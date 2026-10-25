using System.ComponentModel;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class SessionTools
{
    [McpServerTool(Name = "attach")]
    [Description("Attach to the running VS 2026 instance and open a debug session. Returns a sessionId scoping all later calls.")]
// TODO: add ILogger when tools become instance classes; static entry points keep no logger by design.
    public static Task<AttachResult> AttachAsync(
        IDebugBridge bridge,
        [Description("Optional explicit VS instance override. Omit to bind the newest running VS 2026.")]
        string? instanceOverride,
        CancellationToken cancellationToken)
        => ToolErrors.MapAsync(() => bridge.AttachAsync(instanceOverride, cancellationToken));

    [McpServerTool(Name = "list_instances")]
    [Description("List running VS 2026 instances with solution and project names. Session-less and read-only; attach with the entry's display name.")]
    public static Task<ListInstancesResult> ListInstancesAsync(
        IDebugBridge bridge,
        CancellationToken cancellationToken)
        => ToolErrors.MapAsync(() => bridge.ListInstancesAsync(cancellationToken));

    [McpServerTool(Name = "list_sessions")]
    [Description("List live debug sessions (one per attached VS instance). Session-less and read-only; join with list_instances for solution details.")]
    public static Task<ListSessionsResult> ListSessionsAsync(
        IDebugBridge bridge,
        CancellationToken cancellationToken)
        => ToolErrors.MapAsync(() => bridge.ListSessionsAsync(cancellationToken));

    [McpServerTool(Name = "detach")]
    [Description("Release one debug session, freeing its VS binding. Other sessions are unaffected.")]
    public static Task<DetachResult> DetachAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        CancellationToken cancellationToken)
        => ToolPipeline.RunSessionAsync(sessionId, () => bridge.DetachAsync(sessionId, cancellationToken));

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
        [Description("Optional expected process name (from list_processes); recycled PIDs naming something else fail closed.")] string? expectedProcessName = null,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateProcessId(processId);
            ArgumentValidator.ValidateTimeoutMs(timeoutMs);
        }, () => bridge.AttachProcessAsync(sessionId, processId, consent, timeoutMs, cancellationToken, expectedProcessName));

    [McpServerTool(Name = "launch_process")]
    [Description("Start a .NET target process with the debugger attached from birth, bound to this debug session. Breakpoints set before launch bind at startup. Waits up to timeoutMs for a first break.")]
    public static Task<LaunchProcessResult> LaunchProcessAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Absolute path to the target executable (not devenv).")] string executablePath,
        [Description("Command-line arguments passed verbatim (no shell expansion).")] string? arguments = null,
        [Description("Working directory. Defaults to the executable's directory.")] string? workingDirectory = null,
        [Description("Extra environment variables merged over the inherited environment.")] Dictionary<string, string>? environment = null,
        [Description("Budget in milliseconds for start plus the wait for a first break (default 10000).")] int timeoutMs = 10000,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateExecutablePath(executablePath);
            ArgumentValidator.ValidateTimeoutMs(timeoutMs);
        }, () => bridge.LaunchProcessAsync(
            sessionId, executablePath, arguments, workingDirectory, environment, timeoutMs, cancellationToken));
}
