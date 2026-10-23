using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class InspectionTools
{
    // TODO: add ILogger when tools become instance classes; static entry points keep no logger by design.
    [McpServerTool(Name = "read_stack")]
    [Description("Read the call stack of the active session, newest frame first. Requires break mode.")]
    public static Task<ReadStackResult> ReadStackAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Maximum frames to return (default 20).")] int maxFrames = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentValidator.ValidateSessionId(sessionId);
        ArgumentValidator.ValidateMaxFrames(maxFrames);
        return ToolErrors.MapAsync(() => bridge.ReadStackAsync(sessionId, maxFrames, cancellationToken));
    }

    [McpServerTool(Name = "read_variables")]
    [Description("Read a frame's locals and arguments in the active session; pass an expandable path to drill into nested members of that frame. Requires break mode. Never moves evaluate off the top frame.")]
    public static Task<ReadVariablesResult> ReadVariablesAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Frame id returned by read_stack.")] string frameId,
        [Description("Expandable path from a previous read_variables result (read-only, same rule as evaluate); omit for the frame's top level.")] string? path = null,
        [Description("Maximum variables to return (default 50).")] int maxVariables = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentValidator.ValidateSessionId(sessionId);
        ArgumentValidator.ValidateFrameId(frameId);
        ArgumentValidator.ValidatePath(path);
        ArgumentValidator.ValidateMaxVariables(maxVariables);
        return ToolErrors.MapAsync(() => bridge.ReadVariablesAsync(sessionId, frameId, path, maxVariables, cancellationToken));
    }

    [McpServerTool(Name = "evaluate")]
    [Description("Evaluate a read-only expression in the top frame (f0) of the active session. Requires break mode. Assignment, ++/--, and statements are rejected; calls with side effects are refused.")]
    public static Task<EvaluateResult> EvaluateAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Read-only expression to evaluate.")] string expression,
        [Description("Budget in milliseconds for the evaluation (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        ArgumentValidator.ValidateSessionId(sessionId);
        ArgumentValidator.ValidateExpression(expression);
        ArgumentValidator.ValidateTimeoutMs(timeoutMs);
        return ToolErrors.MapAsync(() => bridge.EvaluateAsync(sessionId, expression, timeoutMs, cancellationToken));
    }
}
