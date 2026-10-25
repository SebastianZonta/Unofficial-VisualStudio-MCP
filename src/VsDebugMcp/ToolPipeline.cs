namespace VsDebugMcp;

/// <summary>Shared validate-then-map entry shape for session-bound tools.
/// Session-id validation always runs first; extra guards keep call-site order;
/// bridge failures map at the boundary via <see cref="ToolErrors"/>.</summary>
internal static class ToolPipeline
{
    public static Task<T> RunSessionAsync<T>(string sessionId, Func<Task<T>> op)
    {
        ArgumentValidator.ValidateSessionId(sessionId);
        return ToolErrors.MapAsync(op);
    }

    public static Task<T> RunSessionAsync<T>(string sessionId, Action validateRest, Func<Task<T>> op)
    {
        ArgumentValidator.ValidateSessionId(sessionId);
        validateRest();
        return ToolErrors.MapAsync(op);
    }

    public static Task<T> RunSessionTimeoutAsync<T>(string sessionId, int timeoutMs, Func<Task<T>> op)
        => RunSessionAsync(sessionId, () => ArgumentValidator.ValidateTimeoutMs(timeoutMs), op);

    public static Task<T> RunSessionFileLineAsync<T>(string sessionId, string file, int line, Action validateRest, Func<Task<T>> op)
        => RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateFile(file);
            ArgumentValidator.ValidateLine(line);
            validateRest();
        }, op);

    public static Task<T> RunSessionFrameAsync<T>(string sessionId, string frameId, Action validateRest, Func<Task<T>> op)
        => RunSessionAsync(sessionId, () =>
        {
            ArgumentValidator.ValidateFrameId(frameId);
            validateRest();
        }, op);
}
