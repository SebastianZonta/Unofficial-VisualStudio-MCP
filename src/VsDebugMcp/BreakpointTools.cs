using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class BreakpointTools
{
    [McpServerTool(Name = "set_breakpoint")]
    [Description("Set a file/line breakpoint in the active session. Returns immediately with bind state; an unbound breakpoint carries a warning, never an error.")]
    public static Task<SetBreakpointResult> SetBreakpointAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Source file path, as shown in the solution.")] string file,
        [Description("1-based line number.")] int line,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file))
            throw new McpException("invalid-argument: file must not be empty; remediation: pass a source file path");
        if (line <= 0)
            throw new McpException("invalid-argument: line must be >= 1; remediation: pass a 1-based line number");
        return ToolErrors.MapAsync(() => bridge.SetBreakpointAsync(sessionId, file, line, cancellationToken));
    }

    [McpServerTool(Name = "list_breakpoints")]
    [Description("List all breakpoints in the active session with bind states. Poll until unbound breakpoints flip to bound.")]
    public static Task<ListBreakpointsResult> ListBreakpointsAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        CancellationToken cancellationToken)
        => ToolErrors.MapAsync(() => bridge.ListBreakpointsAsync(sessionId, cancellationToken));

    [McpServerTool(Name = "remove_breakpoint")]
    [Description("Remove a breakpoint by id in the active session.")]
    public static Task<RemoveBreakpointResult> RemoveBreakpointAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Breakpoint id returned by set_breakpoint.")] string breakpointId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(breakpointId))
            throw new McpException("invalid-argument: breakpointId must not be empty; remediation: call list_breakpoints for valid ids");
        return ToolErrors.MapAsync(() => bridge.RemoveBreakpointAsync(sessionId, breakpointId, cancellationToken));
    }
}
