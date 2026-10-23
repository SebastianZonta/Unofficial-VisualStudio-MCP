# 01: Seam contract plus fake for conditions, hit counts, logpoints

**What to build:** the `IDebugBridge` seam learns optional condition, hit-count mode, and logpoint message on breakpoint set, with the `FakeDebugBridge` emulating evaluate-then-decide per hit plus read-only validation, so conditional behavior is testable with no VS.

**Blocked by:** None (can start immediately).

**Status:** ready-for-human

- [x] Unconditional file/line breakpoints behave exactly as today (back-compat).
- [x] Write-like conditions/messages are rejected at set time with nothing created; `list_breakpoints` shows options plus current hit counts.
- [x] Breakpoints (including log output) are isolated per debug session; `detach` releases them.
- [x] Logpoint hits never change mode nor `lastHit`.

## Comments

Implemented: IDebugBridge.SetBreakpointAsync takes optional condition/hitCount/hitCountMode (exact/every)/logMessage; BreakpointInfo carries the options + HitCount; ListBreakpointsResult carries session Log. FakeDebugBridge emulates evaluate-then-decide per hit in creation order (counter advances per pass; logpoints append and never stop). Fake truthiness: only false/0 is false. 13 new tests in ConditionalBreakpointTests.cs (121 total green).
