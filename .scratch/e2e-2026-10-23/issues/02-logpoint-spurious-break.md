# 02: Logpoint-only launch spuriously breaks once (loop scenario)

**What broke (E2E 2026-10-23-2217):** C6 — launch `loop` with only a logpoint (`i={i} total={total}` at TICK) stopped in `break` at `Scenarios.cs:19` with `i=13`, instead of running to `design`. Reproduced deterministically in a second fresh session (ids reset, log empty): same single stop at `i=13` with 12 log entries. After resume the run completed and the log content was exactly right (20 entries, correct texts/order, `hitCount` 1–20, all ids = logpoint id).

**Contrast:** C10 (threads logpoint `{id}/{n}`) never stopped — so only the "never stops" expectation fails; all log-content sub-checks pass. Suspect: tracepoint created without continue-execution in this path, or a stale/duplicate breakpoint binding at TICK.

**Blocked by:** None.

**Status:** resolved

- [ ] C6 green: logpoint-only launch runs to `design` with the exact 20-entry log.
- [ ] C10 stays green (no regression on the threads logpoint).
- [ ] Unit/live-check coverage for logpoint-never-stops on the loop scenario.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-23-2217.md` row C6.
