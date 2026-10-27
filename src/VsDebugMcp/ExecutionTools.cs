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
        => ToolPipeline.RunSessionAsync(sessionId, () => bridge.PauseAsync(sessionId, cancellationToken));

    [McpServerTool(Name = "resume")]
    [Description("Resume execution in the active session until a breakpoint or the timeout. An over-budget wait returns a timeout status, never a hang. An earlier or foreign (other<N>) breakpoint hit wins.")]
    public static Task<ExecutionResult> ResumeAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Budget in milliseconds for the wait (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
        => ToolPipeline.RunSessionTimeoutAsync(sessionId, timeoutMs, () => bridge.ResumeAsync(sessionId, timeoutMs, cancellationToken));

    [McpServerTool(Name = "run_to_cursor")]
    [Description("Run the debuggee in the active session to a file/line via a temporary breakpoint, removed on every exit. Requires break or run mode; an earlier or foreign (other<N>) breakpoint hit wins; an over-budget wait returns like resume.")]
    public static Task<ExecutionResult> RunToCursorAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Source file path, as shown in the solution.")] string file,
        [Description("1-based line number.")] int line,
        [Description("Budget in milliseconds for the wait (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionFileLineAsync(sessionId, file, line,
            () => ArgumentValidator.ValidateTimeoutMs(timeoutMs),
            () => bridge.RunToCursorAsync(sessionId, file, line, timeoutMs, cancellationToken));

    [McpServerTool(Name = "step")]
    [Description("Step the debuggee in the active session. Direction is over, into, or out. Requires break mode; an over-budget wait returns a timeout status.")]
    public static Task<ExecutionResult> StepAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Step direction: over, into, or out.")] string direction,
        [Description("Budget in milliseconds for the wait (default 5000).")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateDirection(direction);
            ArgumentValidator.ValidateTimeoutMs(timeoutMs);
        }, () => bridge.StepAsync(sessionId, direction, timeoutMs, cancellationToken));

    [McpServerTool(Name = "list_exception_settings")]
    [Description("List the session's CLR exception-break settings: first-chance (break when thrown) or unhandled-only. Read-only; any mode, including run.")]
    public static Task<ListExceptionSettingsResult> ListExceptionSettingsAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Optional case-insensitive substring filter on the exception type name.")] string? nameFilter = null,
        CancellationToken cancellationToken = default)
        => ToolPipeline.RunSessionAsync(sessionId, () => bridge.ListExceptionSettingsAsync(sessionId, nameFilter, cancellationToken));

    [McpServerTool(Name = "set_exception_setting")]
    [Description("Set one CLR exception type's break mode: first-chance, unhandled-only, or default (restores the setting this session found). Requires break or design mode, never run.")]
    public static Task<SetExceptionSettingResult> SetExceptionSettingAsync(
        IDebugBridge bridge,
        [Description("Session id returned by attach.")] string sessionId,
        [Description("Fully qualified CLR exception type name, e.g. System.NullReferenceException.")] string exceptionType,
        [Description("Break mode: first-chance, unhandled-only, or default.")] string mode,
        CancellationToken cancellationToken = default) =>
        ToolPipeline.RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateExceptionType(exceptionType);
            ArgumentValidator.ValidateExceptionMode(mode);
        }, () => bridge.SetExceptionSettingAsync(sessionId, exceptionType.Trim(), mode, cancellationToken));
}