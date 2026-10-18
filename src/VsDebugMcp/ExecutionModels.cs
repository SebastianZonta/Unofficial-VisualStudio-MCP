namespace VsDebugMcp;

/// <summary>Execution-control result. Status is "ok" or "timeout": an over-budget wait is data, never a hang.</summary>
public sealed record ExecutionResult(string Mode, string? LastHit, string Status, string? Note);
