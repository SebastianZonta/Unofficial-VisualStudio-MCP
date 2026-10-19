namespace VsDebugMcp;

/// <summary>Machine-readable error codes used at the tool boundary.
/// Wire format is "{code}: {detail}; remediation: {remediation}".</summary>
public static class ErrorCodes
{
    public const string InvalidArgument = "invalid-argument";
    public const string NoSession = "no-session";
    public const string StaleSession = "stale-session";
    public const string NotRunning = "not-running";
    public const string NotInBreakMode = "not-in-break-mode";
    public const string AlreadyRunning = "already-running";
    public const string BreakpointNotFound = "breakpoint-not-found";
    public const string FrameNotFound = "frame-not-found";
    public const string PathNotFound = "path-not-found";
    public const string InvalidExpression = "invalid-expression";
    public const string Timeout = "timeout";
    public const string VsNotRunning = "vs-not-running";
    public const string ConsentRequired = "consent-required";
    public const string ProcessNotFound = "process-not-found";
    public const string UserMismatch = "user-mismatch";

    /// <summary>Key under which <see cref="DebugDomainException.Code"/> is preserved
    /// in <c>McpException.Data</c> by <see cref="ToolErrors"/>.</summary>
    public const string DataKey = "code";
}

/// <summary>Debugger mode wire values: "design", "run", "break".</summary>
public enum DebuggerMode
{
    Design,
    Run,
    Break,
}

/// <summary>Execution wait status wire values: "ok", "timeout".</summary>
public enum ExecutionStatus
{
    Ok,
    Timeout,
}

public static class DebuggerModeExtensions
{
    public static string ToWireString(this DebuggerMode mode) => mode switch
    {
        DebuggerMode.Design => "design",
        DebuggerMode.Run => "run",
        DebuggerMode.Break => "break",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static ExecutionStatus ToExecutionStatus(this string status) => status switch
    {
        "ok" => ExecutionStatus.Ok,
        "timeout" => ExecutionStatus.Timeout,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string ToWireString(this ExecutionStatus status) => status switch
    {
        ExecutionStatus.Ok => "ok",
        ExecutionStatus.Timeout => "timeout",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
