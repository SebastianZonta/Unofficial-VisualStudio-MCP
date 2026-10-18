using ModelContextProtocol;

namespace VsDebugMcp;

/// <summary>Maps bridge-seam domain failures to tool-boundary <c>McpException</c>s.
/// Cancellation and unexpected failures pass through untouched.</summary>
internal static class ToolErrors
{
    public static async Task<T> MapAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (DebugDomainException ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
