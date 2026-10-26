namespace VsDebugMcp;

/// <summary>Standard result envelope: every tool result carries debugger mode + last-hit.</summary>
public sealed record AttachResult(string SessionId, string Mode, string? LastHit);

/// <summary>Single home for debugger-mode gates: one throw site for both bridges.</summary>
internal static class DebuggerModes
{
    public static void Require(string mode, string code, string detail, string remediation, params string[] allowed)
    {
        if (!allowed.Contains(mode, StringComparer.Ordinal))
            throw DebugDomainException.Fail(code, detail, remediation);
    }

    public static void RequireNot(string mode, string forbidden, string code, string detail, string remediation)
    {
        if (mode == forbidden)
            throw DebugDomainException.Fail(code, detail, remediation);
    }

    public static void RequireRunMode(string mode) =>
        Require(mode, ErrorCodes.NotRunning, $"debuggee is {mode}, nothing to pause", "resume first, then pause while running", "run");

    public static void RequireNotRunMode(string mode) =>
        RequireNot(mode, "run", ErrorCodes.AlreadyRunning, "debuggee is already running", "pause first to inspect");

    public static void RequireBreakMode(string mode) =>
        Require(mode, ErrorCodes.NotInBreakMode, $"debuggee is {mode}",
            "resume to a breakpoint or pause first; only inspect and step in break mode", "break");

    public static void RequireStepDirection(string direction) =>
        Require(direction, ErrorCodes.InvalidArgument, $"unknown step direction {direction}", "pass over, into, or out",
            "over", "into", "out");
}

/// <summary>Single home for trim/substring/order list rules: haystacks and ordering stay with the caller.</summary>
internal static class ListFiltering
{
    public static IReadOnlyList<T> FilterAndOrder<T>(IEnumerable<T> items, string? filter, Func<T, IEnumerable<string>> haystacks, Func<IEnumerable<T>, IEnumerable<T>> order)
    {
        var f = filter?.Trim();
        return order(items.Where(t => string.IsNullOrEmpty(f) || haystacks(t).Any(h => h.Contains(f, StringComparison.OrdinalIgnoreCase)))).ToList();
    }
}
