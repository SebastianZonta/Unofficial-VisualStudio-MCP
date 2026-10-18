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
        => ToolErrors.MapAsync(() => bridge.ReadStackAsync(sessionId, maxFrames, cancellationToken));

    [McpServerTool(Name = "read_variables")]
    [Description("Read a frame's locals and arguments in the active session; pass an expandable path to drill into nested members. Requires break mode.")]
    public static Task<ReadVariablesResult> ReadVariablesAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Frame id returned by read_stack.")] string frameId,
        [Description("Expandable path from a previous read_variables result; omit for the frame's top level.")] string? path = null,
        [Description("Maximum variables to return (default 50).")] int maxVariables = 50,
        CancellationToken cancellationToken = default)
        => ToolErrors.MapAsync(() => bridge.ReadVariablesAsync(sessionId, frameId, path, maxVariables, cancellationToken));

    [McpServerTool(Name = "evaluate")]
    [Description("Evaluate a read-only expression in the active session. Requires break mode. Assignment and statement execution are not supported.")]
    public static Task<EvaluateResult> EvaluateAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Read-only expression to evaluate.")] string expression,
        [Description("Budget in milliseconds for the evaluation (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new McpException("invalid-argument: expression must not be empty; remediation: pass a read-only expression");
        if (expression.Contains(';'))
            throw new McpException("invalid-argument: statements are not supported; remediation: pass a single read-only expression without ';'");
        return ToolErrors.MapAsync(() => bridge.EvaluateAsync(sessionId, expression, timeoutMs, cancellationToken));
    }
}
