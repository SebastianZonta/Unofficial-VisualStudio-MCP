namespace VsDebugMcp;

/// <summary>Expected debugger failure with a machine-readable code plus human remediation.
/// Thrown at the bridge seam; tools map it to <c>McpException</c> at the tool boundary.</summary>
public sealed class DebugDomainException(string code, string detail, string remediation, Exception? inner = null)
    : InvalidOperationException($"{code}: {detail}; remediation: {remediation}", inner)
{
    public string Code { get; } = code;

    /// <summary>Single factory for every domain failure: one <c>new</c> site.</summary>
    public static DebugDomainException Fail(string code, string detail, string remediation) => new(code, detail, remediation);
}
