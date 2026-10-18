using System.ComponentModel;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class ExecutionTools
{
    [McpServerTool(Name = "pause")]
    [Description("Pause the running debuggee in the active session. Fails with not-running unless it is running.")]
    public static Task<ExecutionResult> PauseAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        CancellationToken cancellationToken)
        => ToolErrors.MapAsync(() => bridge.PauseAsync(sessionId, cancellationToken));

    [McpServerTool(Name = "resume")]
    [Description("Resume execution in the active session until a breakpoint or the timeout. An over-budget wait returns a timeout status, never a hang.")]
    public static Task<ExecutionResult> ResumeAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Budget in milliseconds for the wait (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
        => ToolErrors.MapAsync(() => bridge.ResumeAsync(sessionId, timeoutMs, cancellationToken));

    [McpServerTool(Name = "step")]
    [Description("Step the debuggee in the active session. Direction is over, into, or out. Requires break mode; an over-budget wait returns a timeout status.")]
    public static Task<ExecutionResult> StepAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Step direction: over, into, or out.")] string direction,
        [Description("Budget in milliseconds for the wait (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        if (direction is not ("over" or "into" or "out"))
            throw new ModelContextProtocol.McpException(
                $"invalid-argument: direction must be over, into, or out; remediation: pass one of the three directions");
        return ToolErrors.MapAsync(() => bridge.StepAsync(sessionId, direction, timeoutMs, cancellationToken));
    }
}
