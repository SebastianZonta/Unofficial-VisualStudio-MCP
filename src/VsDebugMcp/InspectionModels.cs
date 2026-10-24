namespace VsDebugMcp;

public sealed record StackFrameInfo(string Id, string Function, string Module, string? File, int? Line);

public sealed record ValueInfo(string Name, string Type, string Value, bool Expandable, string? Path);

public sealed record ReadStackResult(IReadOnlyList<StackFrameInfo> Frames, string Mode, string? LastHit);

public sealed record ReadVariablesResult(string FrameId, string? Path, IReadOnlyList<ValueInfo> Variables, string Mode, string? LastHit);

/// <summary>Echo of one consented write: Value and Type are re-read after the write.</summary>
public sealed record SetVariableResult(string FrameId, string Path, string Value, string Type, string Mode, string? LastHit);

public sealed record EvaluateResult(string Expression, string Value, string Type, bool Expandable, string Mode, string? LastHit);
