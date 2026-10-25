# Duplication / extraction audit — tools layer (Session/Breakpoint/Execution/Inspection)

Scope: `src/VsDebugMcp/SessionTools.cs`, `src/VsDebugMcp/BreakpointTools.cs`, `src/VsDebugMcp/ExecutionTools.cs`, `src/VsDebugMcp/InspectionTools.cs`. No edits made.

## Summary table

| # | Location(s) | Pattern | Proposal | Est. saved |
|---|---|---|---|---|
| D1 | 17×: Session 40-42,64-67,82-86; Breakpoint 24-28,38-39,50-51,62-64; Execution 18-19,30-32,45-49,61-64,75-76,88-91; Inspection 19-21,34-38,52-56,68-71 | `ValidateSessionId(sessionId);` + `return ToolErrors.MapAsync(() => bridge.XAsync(...))` | `private static Task<T> RunSessionAsync<T>(string sessionId, Func<Task<T>> op) { ValidateSessionId(sessionId); return ToolErrors.MapAsync(op); }` in one shared `ToolPipeline` static class; call sites become `=> RunSessionAsync(sessionId, () => bridge.XAsync(...))` | ~17 lines (1 line per session-bound method) |
| D2 | 6×: Session 66,84; Execution 31,48,63; Inspection 70 (`ValidateTimeoutMs(timeoutMs)`, always after `ValidateSessionId`) | repeated timeout guard | fold into D1 overload: `RunSessionAsync<T>(string sessionId, int timeoutMs, Func<Task<T>> op)` or single `ValidateSessionAndTimeout(sessionId, timeoutMs)` | ~6 lines if overload used |
| D3 | Breakpoint 24-26 + Execution 45-47: identical 3-line prefix `ValidateSessionId`/`ValidateFile`/`ValidateLine` | file+line guard trio | `ArgumentValidator.ValidateSessionFileLine(sessionId, file, line)` (new overload alongside existing validators) | ~4 lines (2 per site) |
| D4 | Inspection 34-35 + 52-53: `ValidateSessionId` + `ValidateFrameId` | frame guard pair | `ArgumentValidator.ValidateSessionFrame(sessionId, frameId)` | ~2 lines |
| D5 | 17× `[Description("Session id returned by attach.")]` (every session-bound param) + 5× timeout `[Description("Budget in milliseconds ...")]` (Session 60,79; Execution 27,42,58; Inspection 65) | repeated attribute strings | `internal static class ToolDescriptions { public const string SessionId = "Session id returned by attach."; }` then `[Description(ToolDescriptions.SessionId)]`; same for timeout if wording unified | ~0 runtime lines; single source for wording (note: attribute arg must be const — feasible) |
| D6 | 4× identical TODO comment: Session 11; Breakpoint 10; Execution 10; Inspection 10 (`// TODO: add ILogger when tools become instance classes; ...`) | copy-pasted comment | delete 3 of 4 or file one tracking issue and drop all four | 3 lines |
| D7 | Session 17,24,31,51 (`=> ToolErrors.MapAsync(...)` session-less passthrough, expression-bodied) vs all other methods (block-bodied validate+map) | style inconsistency, not duplication | normalize to one shape when D1 lands; no helper needed — one-liners are already minimal | 0 lines |

## Details

### D1 — validate-session + map wrapper (biggest win)
Current (representative, Execution 17-20, Inspection 18-22, Session 39-42 — all identical shape):
```csharp
ArgumentValidator.ValidateSessionId(sessionId);
return ToolErrors.MapAsync(() => bridge.PauseAsync(sessionId, cancellationToken));
```
Proposed (one shared helper, e.g. in `ToolErrors.cs` or new `ToolPipeline.cs`):
```csharp
private static Task<T> RunSessionAsync<T>(string sessionId, Func<Task<T>> op)
{
    ArgumentValidator.ValidateSessionId(sessionId);
    return ToolErrors.MapAsync(op);
}
// call site:
public static Task<ExecutionResult> PauseAsync(...) =>
    RunSessionAsync(sessionId, () => bridge.PauseAsync(sessionId, cancellationToken));
```
Saves ~1 line × 17 sites = ~17 lines and removes the most-copied 2-line block. Applies to every file:line range listed in D1.

### D2 — timeout guard rides along
Current (Session 64-66, Execution 30-31, Inspection 68-70):
```csharp
ArgumentValidator.ValidateSessionId(sessionId);
...
ArgumentValidator.ValidateTimeoutMs(timeoutMs);
```
Sketch:
```csharp
private static Task<T> RunSessionAsync<T>(string sessionId, int timeoutMs, Func<Task<T>> op)
{
    ArgumentValidator.ValidateSessionId(sessionId);
    ArgumentValidator.ValidateTimeoutMs(timeoutMs);
    return ToolErrors.MapAsync(op);
}
```
Only worth it together with D1; standalone `ValidateSessionAndTimeout` in `ArgumentValidator` saves ~6 lines total.

### D3 — session+file+line trio
Breakpoint 24-26:
```csharp
ArgumentValidator.ValidateSessionId(sessionId);
ArgumentValidator.ValidateFile(file);
ArgumentValidator.ValidateLine(line);
```
Execution 45-47: byte-identical (modulo file context). Sketch:
```csharp
public static void ValidateSessionFileLine(string sessionId, string file, int line)
{
    ValidateSessionId(sessionId); ValidateFile(file); ValidateLine(line);
}
```
Saves 2 lines/site × 2 sites = 4 lines.

### D4 — session+frame pair
Inspection 34-35 and 52-53 identical:
```csharp
ArgumentValidator.ValidateSessionId(sessionId);
ArgumentValidator.ValidateFrameId(frameId);
```
Sketch: `ValidateSessionFrame(sessionId, frameId)`. Saves 1 line/site × 2 = 2 lines.

### D5 — repeated Description strings
`[Description("Session id returned by attach.")]` × 17 is the most-repeated literal in scope; timeout descriptions × 5 vary only by default value (`5000` vs `10000` vs `30000`). Const-string extraction is legal for attributes. Savings are maintainability, not line count.

### Negative findings (explicitly checked)
- **if/else → ternary / switch**: none to convert — the four files contain zero conditionals; all branching lives in `ArgumentValidator`/`ToolErrors` (out of scope).
- **Validation/error-mapping logic**: no repeated mapping logic in scope beyond the `MapAsync` one-liner (already minimal); no string formatting (`$"..."`/`string.Format`) at all.
- **Long methods**: none. Longest bodies: `SessionTools.LaunchProcessAsync` (Session 81-87, 6 stmt lines), `AttachProcessAsync` (Session 63-68, 5 lines), `InspectionTools.ReadVariablesAsync` (Inspection 33-39, 6 lines). No split justified.

## Total estimate
D1+D2+D3+D4 ≈ 29 lines saved across the four files for one small helper + two validator overloads. D5/D6 are hygiene (wording single-source, 3 comment lines removed).
