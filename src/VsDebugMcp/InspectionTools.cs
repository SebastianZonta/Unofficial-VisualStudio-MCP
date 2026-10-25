using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class InspectionTools
{
    [McpServerTool(Name = "read_stack")]
    [Description("Read the call stack of the active session, newest frame first. Requires break mode.")]
    public static Task<ReadStackResult> ReadStackAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Maximum frames to return (default 20).")] int maxFrames = 20,
        CancellationToken cancellationToken = default)
        => ToolPipeline.RunSessionAsync(sessionId,
            () => ArgumentValidator.ValidateMaxFrames(maxFrames),
            () => bridge.ReadStackAsync(sessionId, maxFrames, cancellationToken));

    [McpServerTool(Name = "read_variables")]
    [Description("Read a frame's locals and arguments in the active session; pass an expandable path to drill into nested members of that frame. Requires break mode. Never moves evaluate off the top frame.")]
    public static Task<ReadVariablesResult> ReadVariablesAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Frame id returned by read_stack.")] string frameId,
        [Description("Expandable path from a previous read_variables result (read-only, same rule as evaluate); omit for the frame's top level.")] string? path = null,
        [Description("Maximum variables to return (default 50).")] int maxVariables = 50,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionFrameAsync(sessionId, frameId, () =>
        {
            ArgumentValidator.ValidatePath(path);
            ArgumentValidator.ValidateMaxVariables(maxVariables);
        }, () => bridge.ReadVariablesAsync(sessionId, frameId, path, maxVariables, cancellationToken));

    [McpServerTool(Name = "set_variable")]
    [Description("Write one variable in a frame of the active session. Requires break mode and consent:true on every call. Address it by its read_variables path (or its name at top level); value uses the read_variables representation.")]
    public static Task<SetVariableResult> SetVariableAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Frame id returned by read_stack.")] string frameId,
        [Description("Variable to write: its read_variables path, or its name for a top-level local or argument.")] string path,
        [Description("New value as read_variables shows it: number, true/false, 'c', \"quoted string\", or null.")] string value,
        [Description("Explicit consent to mutate target state. Must be true on every call; attach_process consent never counts.")] bool consent = false,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionFrameAsync(sessionId, frameId, () =>
        {
            ArgumentValidator.ValidateWritePath(path);
            ArgumentValidator.ValidateWriteValue(value);
        }, () => bridge.SetVariableAsync(sessionId, frameId, path, value, consent, cancellationToken));

    [McpServerTool(Name = "evaluate")]
    [Description("Evaluate a read-only expression in the top frame (f0) of the active session. Requires break mode. Assignment, ++/--, and statements are rejected; calls with side effects are refused.")]
    public static Task<EvaluateResult> EvaluateAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Read-only expression to evaluate.")] string expression,
        [Description("Budget in milliseconds for the evaluation (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateExpression(expression);
            ArgumentValidator.ValidateTimeoutMs(timeoutMs);
        }, () => bridge.EvaluateAsync(sessionId, expression, timeoutMs, cancellationToken));
}
