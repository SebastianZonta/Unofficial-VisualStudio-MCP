namespace VsDebugMcp;

/// <summary>Machine-readable error codes used at the tool boundary.
/// Wire format is "{code}: {detail}; remediation: {remediation}".</summary>
public static class ErrorCodes
{
    public const string InvalidArgument = "invalid-argument";
    public const string NoSession = "no-session";
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
    public const string ExecutableNotFound = "executable-not-found";
    public const string AlreadyBound = "already-bound";
    public const string LaunchFailed = "launch-failed";

    /// <summary>Key under which <see cref="DebugDomainException.Code"/> is preserved
    /// in <c>McpException.Data</c> by <see cref="ToolErrors"/>.</summary>
    public const string DataKey = "code";
}
