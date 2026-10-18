using System.Runtime.InteropServices;

namespace VsDebugMcp;

/// <summary>Maps raw COM failures to named domain errors with remediation. Pure function; testable without VS.</summary>
internal static class ComErrorMapper
{
    private const int RpcECallRejected = unchecked((int)0x80010001); // VS busy: modal dialog or blocked UI thread
    private const int EAccessDenied = unchecked((int)0x80070005); // integrity-level / elevation mismatch

    public static DebugDomainException ToDomain(COMException ex) => ex.HResult switch
    {
        RpcECallRejected => new DebugDomainException(
            "vs-busy", $"Visual Studio rejected the automation call (HRESULT 0x{RpcECallRejected:X8}; modal dialog or busy UI thread)",
            "dismiss any modal dialog in VS and retry"),
        EAccessDenied => new DebugDomainException(
            "elevation-mismatch", $"access denied talking to Visual Studio (HRESULT 0x{EAccessDenied:X8}; integrity-level mismatch)",
            "restart the bridge at the same elevation as VS (both elevated or both not)"),
        var hr => new DebugDomainException(
            "automation-failed", $"VS automation call failed (HRESULT 0x{hr:X8}): {ex.Message}",
            "dismiss any modal dialog in VS and retry; if it persists, restart VS"),
    };
}
