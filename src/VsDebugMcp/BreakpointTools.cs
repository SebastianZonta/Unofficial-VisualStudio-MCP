using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace VsDebugMcp;

[McpServerToolType]
public sealed class BreakpointTools
{
    [McpServerTool(Name = "set_breakpoint")]
    [Description("Set a file/line breakpoint in the active session. Returns immediately with bind state; an unbound breakpoint carries a warning, never an error. Optional condition (read-only expression, break only when true), hitCount with hitCountMode exact/every, or logMessage template with {expression} holes (a logpoint never stops).")]
    public static Task<SetBreakpointResult> SetBreakpointAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Source file path, as shown in the solution.")] string file,
        [Description("1-based line number.")] int line,
        CancellationToken cancellationToken,
        [Description("Read-only condition expression; break only when true. Omit for unconditional.")] string? condition = null,
        [Description("Positive hit count N; break on the Nth hit (exact) or every Nth hit (every).")] int? hitCount = null,
        [Description("Hit-count mode: exact (default) or every.")] string? hitCountMode = null,
        [Description("Logpoint message template with {expression} holes; never stops. Incompatible with condition/hitCount.")] string? logMessage = null) =>
        ToolPipeline.RunSessionFileLineAsync(sessionId, file, line,
            () => ArgumentValidator.ValidateBreakpointOptions(condition, hitCount, hitCountMode, logMessage),
            () => bridge.SetBreakpointAsync(sessionId, file, line, cancellationToken, condition, hitCount, hitCountMode, logMessage));

    [McpServerTool(Name = "list_breakpoints")]
    [Description("List all breakpoints in the active session with bind states. Poll until unbound breakpoints flip to bound. Breakpoints this session did not create show as list-only other<N> ids.")]
    public static Task<ListBreakpointsResult> ListBreakpointsAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        CancellationToken cancellationToken)
        => ToolPipeline.RunSessionAsync(sessionId, () => bridge.ListBreakpointsAsync(sessionId, cancellationToken));

    [McpServerTool(Name = "list_modules")]
    [Description("List the modules of the session's target process with loaded flag and symbol state (loaded/missing/failed/unknown). Read-only; requires break or run mode. First check when a breakpoint stays unbound.")]
    public static Task<ListModulesResult> ListModulesAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Optional case-insensitive substring filter on module name or path.")] string? nameFilter = null,
        CancellationToken cancellationToken = default)
        => ToolPipeline.RunSessionAsync(sessionId, () => bridge.ListModulesAsync(sessionId, nameFilter, cancellationToken));

    [McpServerTool(Name = "remove_breakpoint")]
    [Description("Remove a breakpoint this session created, by the stable id set_breakpoint returned.")]
    public static Task<RemoveBreakpointResult> RemoveBreakpointAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Breakpoint id returned by set_breakpoint.")] string breakpointId,
        CancellationToken cancellationToken)
        => ToolPipeline.RunSessionAsync(sessionId,
            () => ArgumentValidator.ValidateBreakpointId(breakpointId),
            () => bridge.RemoveBreakpointAsync(sessionId, breakpointId, cancellationToken));
}
