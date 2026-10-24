namespace VsDebugMcp;

/// <summary>Hit-count modes: "exact" breaks on the Nth hit only; "every" breaks every Nth hit.</summary>
public static class HitCountModes
{
    public const string Exact = "exact";
    public const string Every = "every";
}

public sealed record BreakpointInfo(
    string Id, string File, int Line, bool Bound, string? Warning,
    string? Condition = null, int? HitCountTarget = null, string? HitCountMode = null,
    int HitCount = 0, string? LogMessage = null);

public sealed record BreakpointLogEntry(string BreakpointId, string Message, int HitCount);

public sealed record SetBreakpointResult(BreakpointInfo Breakpoint, string Mode, string? LastHit, bool Created = true);

public sealed record ListBreakpointsResult(
    IReadOnlyList<BreakpointInfo> Breakpoints, string Mode, string? LastHit,
    IReadOnlyList<BreakpointLogEntry>? Log = null);

public sealed record RemoveBreakpointResult(string RemovedId, string Mode, string? LastHit);
