namespace VsDebugMcp;

/// <summary>Target process: a local OS process the debugger binds to through attach.
/// Distinct from a VS instance (the debugger side): a <c>devenv</c> entry is never an attach target.</summary>
public sealed record TargetProcessInfo(
    int Pid,
    string Name,
    string Owner,
    bool Mine,
    string? Error);

/// <summary>Session-less snapshot of local target-process candidates for agent-side matching.</summary>
public sealed record ListProcessesResult(IReadOnlyList<TargetProcessInfo> Processes);
