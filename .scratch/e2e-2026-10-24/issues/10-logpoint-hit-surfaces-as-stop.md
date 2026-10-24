# 10: Logpoint hit surfaces as a stop on multi-thread launch (C10)

**What broke (E2E 2026-10-24-1800-R1R, C10, clean suo):**
`launch_process("threads")` with logpoint `{id}/{n}` at WORKER:83 →
`{mode:"break", lastHit:...Scenarios.cs:83, note:"hit breakpoint; logged 1
logpoint hits"}` — the debuggee STOPPED at the logpoint, against the
PollResume contract "logpoint hit never surfaces as a stop". Follow-up
`resume` → `design`, and the Log held exactly the 20 pairs 1/1…4/5 once each
(data assertion held; only "never stops" failed). No retry (assertion, not
vs-busy/timeout).

**Suspected:** multi-thread logpoint race — 4 threads hammering WORKER, a
second observed break misclassified as a genuine stop after a transient
TryLogAndContinue miss. First clean-state run of C10 (R1's C10 was BLOCKED on
stale suo), so unknown whether timing-flaky or deterministic.

**Supplements:** post-run-end phantom `other1` twin of `bp6` at the same line
(VS recreating the breakpoint COM object on session end, breaking the
IUnknown-identity match); `remove_breakpoint(bp6)` cleared both, list empty.
Relevant to Z1-style flows: after a run-end, expect a possible transient
twin until the owned id is removed.

**Blocked by:** None.

**Status:** needs-triage

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R1R.md` rows C10,
C10-supplement. Re-run C10 alone to check flakiness before root-causing.
