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
