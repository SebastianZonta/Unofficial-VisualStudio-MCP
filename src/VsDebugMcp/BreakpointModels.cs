namespace VsDebugMcp;

public sealed record BreakpointInfo(string Id, string File, int Line, bool Bound, string? Warning);

public sealed record SetBreakpointResult(BreakpointInfo Breakpoint, string Mode, string? LastHit);

public sealed record ListBreakpointsResult(IReadOnlyList<BreakpointInfo> Breakpoints, string Mode, string? LastHit);

public sealed record RemoveBreakpointResult(string RemovedId, string Mode, string? LastHit);
