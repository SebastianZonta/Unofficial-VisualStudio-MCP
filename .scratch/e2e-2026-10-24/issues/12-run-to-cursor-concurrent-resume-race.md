# 12: run_to_cursor loses a concurrent resume race (E27, automation-failed 0x89711007)

**What broke (E2E 2026-10-24-1953-R3, E27, 2 attempts):**
Breakpoint at HEARTBEAT + `run_to_cursor(HEARTBEAT)` issued concurrently with a
second `resume` → `run_to_cursor` rejected both times with
`automation-failed: VS automation call failed (HRESULT 0x89711007): Unable to
execute method at this time`. The concurrent `resume` won (attempt 1: `break`
at Scenarios.cs:134 via the persistent bp; attempt 2: `mode: run`). No leaked
temp breakpoint either time, never `already-running` — but the expected `break`
from `run_to_cursor` was not met, so the case FAILs.

**Suspected:** COM-STA contention vs debugger-state handling in the
`RunToCursor` List–Set window: the concurrent `resume` takes the debugger run
state first and the `run_to_cursor` automation call has no retry/survive path.
Needs code-level follow-up (R3 did not determine which layer rejects).

**Evidence:** `.scratch/e2e-runs/2026-10-24-1953-R3.md` (E27 rows + Failures table).

**Status:** open

**Fix:** `RunToCursor.ContinueAsync` now catches the Go-conflict
(`automation-failed` + inner HResult 0x89711007), re-lists, and joins via
`cont("run")` while still debugging; other HResults rethrow. Covered by
`RunToCursor_joins_run_on_go_conflict` /
`RunToCursor_does_not_join_other_automation_failures`
(`tests/VsDebugMcp.Tests/RunToCursorTests.cs`). Live E27 re-pass pending —
flip to `resolved` on a clean pass.

**Research:** `.scratch/e2e-runs/2026-10-24-1953-E27-research.md` — root cause is
unserialized mode transitions (List/Set and `Go` are separate STA dispatches);
recommended fix is retry-on-0x89711007 with re-list + adopt-the-break in
`RunToCursor.ContinueAsync`, unit-testable via `FakeDebugBridge`.
