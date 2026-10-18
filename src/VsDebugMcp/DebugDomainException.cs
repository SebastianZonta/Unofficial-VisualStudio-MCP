namespace VsDebugMcp;

/// <summary>Expected debugger failure with a machine-readable code plus human remediation.
/// Thrown at the bridge seam; tools map it to <c>McpException</c> at the tool boundary.</summary>
public sealed class DebugDomainException(string code, string detail, string remediation)
    : InvalidOperationException($"{code}: {detail}; remediation: {remediation}")
{
    public string Code { get; } = code;
}
