using ModelContextProtocol;

namespace VsDebugMcp;

/// <summary>Tool-boundary argument validation. Throws <c>McpException</c> with an
/// <see cref="ErrorCodes.InvalidArgument"/> code; never touches the bridge.</summary>
public static class ArgumentValidator
{
    public static void ValidateSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new McpException($"{ErrorCodes.InvalidArgument}: sessionId must not be empty; remediation: call attach first");
    }

    public static void ValidateFile(string file)
    {
        if (string.IsNullOrWhiteSpace(file))
            throw new McpException($"{ErrorCodes.InvalidArgument}: file must not be empty; remediation: pass a source file path");
    }

    public static void ValidateLine(int line)
    {
        if (line < 1)
            throw new McpException($"{ErrorCodes.InvalidArgument}: line must be >= 1; remediation: pass a 1-based line number");
    }

    public static void ValidateBreakpointId(string breakpointId)
    {
        if (string.IsNullOrWhiteSpace(breakpointId))
            throw new McpException($"{ErrorCodes.InvalidArgument}: breakpointId must not be empty; remediation: call list_breakpoints for valid ids");
    }

    public static void ValidateBreakpointOptions(string? condition, int? hitCount, string? hitCountMode, string? logMessage)
    {
        var error = BreakpointOptions.Check(condition, hitCount, hitCountMode, logMessage);
        if (error is not null)
            throw new McpException($"{ErrorCodes.InvalidArgument}: {error.Value.Detail}; remediation: {error.Value.Remediation}");
    }

    public static void ValidateFrameId(string frameId)
    {
        if (string.IsNullOrWhiteSpace(frameId))
            throw new McpException($"{ErrorCodes.InvalidArgument}: frameId must not be empty; remediation: call read_stack for valid frame ids");
    }

    /// <summary>A drill-down path reaches the expression evaluator, so it gets the evaluate rule. Null means top level.</summary>
    public static void ValidatePath(string? path)
    {
        if (path is null)
            return;
        var reason = BreakpointOptions.CheckExpression(path);
        if (reason is not null)
            throw new McpException($"{ErrorCodes.InvalidArgument}: path {reason.Value.Detail}; remediation: pass an expandable path returned by read_variables");
    }

    public static void ValidateDirection(string direction)
    {
        if (direction is not ("over" or "into" or "out"))
            throw new McpException($"{ErrorCodes.InvalidArgument}: direction must be over, into, or out; remediation: pass one of the three directions");
    }

    /// <summary>Evaluate shares the single read-only rule (<see cref="BreakpointOptions"/>):
    /// the code follows the first failing gate (see CodeForExpression).</summary>
    public static void ValidateExpression(string expression)
    {
        var reason = BreakpointOptions.CheckExpression(expression);
        if (reason is null)
            return;
        var code = BreakpointOptions.CodeForExpression(expression);
        throw new McpException($"{code}: expression {reason.Value.Detail}; remediation: {reason.Value.Remediation}");
    }

    public static void ValidateTimeoutMs(int timeoutMs, string paramName = "timeoutMs")
    {
        if (timeoutMs is < 1000 or > 60000)
            throw new McpException($"{ErrorCodes.InvalidArgument}: {paramName} must be 1000-60000; remediation: pass a budget in milliseconds within range");
    }

    public static void ValidateMaxFrames(int maxFrames)
    {
        if (maxFrames is < 1 or > 200)
            throw new McpException($"{ErrorCodes.InvalidArgument}: maxFrames must be 1-200; remediation: pass a frame cap within range");
    }

    public static void ValidateMaxVariables(int maxVariables)
    {
        if (maxVariables is < 1 or > 500)
            throw new McpException($"{ErrorCodes.InvalidArgument}: maxVariables must be 1-500; remediation: pass a variable cap within range");
    }

    public static void ValidateProcessId(int processId)
    {
        if (processId <= 0)
            throw new McpException($"{ErrorCodes.InvalidArgument}: processId must be > 0; remediation: pass a local process ID from list_processes");
    }

    public static void ValidateExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
            throw new McpException($"{ErrorCodes.InvalidArgument}: executablePath must be an absolute path; remediation: pass the full path to the target executable");
    }
}
