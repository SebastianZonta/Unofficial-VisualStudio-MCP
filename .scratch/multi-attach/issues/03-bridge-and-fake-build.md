Type: task
Status: resolved
Labels: wayfinder:task
Blocked by: 01, 02

## Question

Build the decided store in `DteDebugBridge.cs` and mirror it in `FakeDebugBridge.cs` (per-session mode/lastHit and breakpoint isolation: a breakpoint set via session A never appears under session B), wire `list_sessions`/`detach` through `SessionTools.cs`, delete `stale-session` from `ToolErrors.cs`/`ErrorCodes.cs`, and cover it with `FakeDebugBridge` tests that run under `dotnet test` with no VS (multi-attach, idempotent re-attach, detach, per-session VS-gone).

## Answer

Built on `feature/multi-attach` exactly as contracted. `DteDebugBridge` holds `Dictionary<sessionId, SessionEntry>` under a lock (`Dte`, `DisplayName`, `Pid`, `Dead`); every call site captures its session DTE after `RequireSessionAsync`; releases (detach, mark-dead, dispose) run on the dispatcher, never the caller thread; COM disconnects (`0x80010108`, `0x800706BA`, `0x800706B5`, `0x8001010A` via `ComErrorMapper.IsDisconnected`) mark only that session dead instead of failing everything. `FakeDebugBridge` mirrors with per-session mode/lastHit/breakpoints plus a `SimulateVsExit` test hook. `IDebugBridge` gains `ListSessionsAsync`/`DetachAsync`; `SessionTools` gains `list_sessions`/`detach`; `ErrorCodes.StaleSession` deleted. Verified: `dotnet build` clean (one pre-existing test warning aside), `dotnet test` 108/108 including rewritten re-attach-idempotency test and new `MultiSessionTests` (distinct sessions, breakpoint/mode isolation, detach lifecycle, unknown-detach, per-session VS-gone, re-attach-after-exit). Pre-existing whitespace-only diffs on `Program.cs`/`ArgumentValidator.cs`/`DteDebugBridge.cs` (from `main`) left untouched.
