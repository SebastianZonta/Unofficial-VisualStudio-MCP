namespace VsDebugMcp;

public sealed record VsInstanceInfo(
    string DisplayName,
    int Pid,
    string Version,
    string SolutionName,
    string SolutionPath,
    IReadOnlyList<string> ProjectNames,
    bool Reachable,
    string? Error);

public sealed record ListInstancesResult(IReadOnlyList<VsInstanceInfo> Instances);

/// <summary>One live debug session: a handle binding one VS instance.</summary>
public sealed record SessionInfo(string SessionId, string DisplayName);

/// <summary>Session-less inventory of live bindings; join with list_instances for solution details.</summary>
public sealed record ListSessionsResult(IReadOnlyList<SessionInfo> Sessions);

/// <summary>Echoes the released session id.</summary>
public sealed record DetachResult(string SessionId);
