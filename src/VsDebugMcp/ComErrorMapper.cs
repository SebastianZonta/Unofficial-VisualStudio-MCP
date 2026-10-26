using System.Runtime.InteropServices;

namespace VsDebugMcp;

/// <summary>Maps raw COM failures to named domain errors with remediation. Pure function; testable without VS.</summary>
internal static class ComErrorMapper
{
    private const int RpcECallRejected = unchecked((int)0x80010001); // VS busy: modal dialog or blocked UI thread
    private const int EAccessDenied = unchecked((int)0x80070005); // integrity-level / elevation mismatch
    private const int RpcEDisconnected = unchecked((int)0x80010108); // server object disconnected (bound VS exited)
    private const int RpcSServerUnavailable = unchecked((int)0x800706BA); // RPC server unavailable (bound VS gone)
    private const int RpcSUnknownIf = unchecked((int)0x800706B5); // endpoint gone with the server
    private const int RpcEServerCallRetryLater = unchecked((int)0x8001010A); // server going away
    private const int EFail = unchecked((int)0x80004005); // evaluator refused the assignment

    /// <summary>True when the failure means the bound VS instance is gone (per-session death, not a busy server).</summary>
    public static bool IsDisconnected(COMException ex) => ex.HResult is
        RpcEDisconnected or RpcSServerUnavailable or RpcSUnknownIf or RpcEServerCallRetryLater;

    /// <summary>True when VS refused the call (busy): nothing happened, so the target is unchanged.</summary>
    public static bool IsCallRejected(COMException ex) => ex.HResult == RpcECallRejected;

    /// <summary>True only for a genuine evaluator refusal (E_FAIL): the only shape mapped
    /// to read-only-target. Busy, denied and disconnects keep their own codes.</summary>
    public static bool IsRefusal(COMException ex) => ex.HResult == EFail;

    public static DebugDomainException ToDomain(COMException ex) => ex.HResult switch
    {
        RpcECallRejected => new DebugDomainException(
            ErrorCodes.VsBusy, $"Visual Studio rejected the automation call (HRESULT 0x{RpcECallRejected:X8}; modal dialog or busy UI thread)",
            "dismiss any modal dialog in VS and retry", ex),
        EAccessDenied => new DebugDomainException(
            "elevation-mismatch", $"access denied talking to Visual Studio (HRESULT 0x{EAccessDenied:X8}; integrity-level mismatch)",
            "restart the bridge at the same elevation as VS (both elevated or both not)", ex),
        var hr => new DebugDomainException(
            ErrorCodes.AutomationFailed, $"VS automation call failed (HRESULT 0x{hr:X8}): {ex.Message}",
            "dismiss any modal dialog in VS and retry; if it persists, restart VS", ex),
    };
}