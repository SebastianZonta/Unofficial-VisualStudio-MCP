namespace VsDebugMcp;

/// <summary>Standard result envelope: every tool result carries debugger mode + last-hit.</summary>
public sealed record AttachResult(string SessionId, string Mode, string? LastHit);
