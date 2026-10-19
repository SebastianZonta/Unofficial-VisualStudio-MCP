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

    public static void ValidateFrameId(string frameId)
    {
        if (string.IsNullOrWhiteSpace(frameId))
            throw new McpException($"{ErrorCodes.InvalidArgument}: frameId must not be empty; remediation: call read_stack for valid frame ids");
    }

    public static void ValidateDirection(string direction)
    {
        if (direction is not ("over" or "into" or "out"))
            throw new McpException($"{ErrorCodes.InvalidArgument}: direction must be over, into, or out; remediation: pass one of the three directions");
    }

    public static void ValidateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new McpException($"{ErrorCodes.InvalidArgument}: expression must not be empty; remediation: pass a read-only expression");
        if (expression.Contains(';'))
            throw new McpException($"{ErrorCodes.InvalidArgument}: statements are not supported; remediation: pass a single read-only expression without ';'");
        if (expression.Contains('\r') || expression.Contains('\n'))
            throw new McpException($"{ErrorCodes.InvalidArgument}: statements are not supported; remediation: pass a single-line read-only expression");
        if (ContainsAssignment(expression))
            throw new McpException($"{ErrorCodes.InvalidArgument}: assignment is not supported; remediation: pass a read-only expression without '='");
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

    private static bool ContainsAssignment(string expression)
    {
        for (var i = 0; i < expression.Length; i++)
        {
            if (expression[i] != '=')
                continue;
            char prev = i > 0 ? expression[i - 1] : '\0';
            char next = i + 1 < expression.Length ? expression[i + 1] : '\0';
            // Allow ==, !=, <=, >=, => comparisons/lambdas; anything else is assignment.
            if (next == '=' || prev == '=' || prev == '!' || prev == '<' || prev == '>' || next == '>')
                continue;
            return true;
        }
        return false;
    }
}
