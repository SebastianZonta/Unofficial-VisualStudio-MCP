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

**Update (solo re-run, same binary, repo Sandbox):** launch went straight to
`design` with no stop — C10 behavior PASS this time, Log complete. So the
R1R launch-stop is a timing flake (1 stop in 2 runs), not deterministic. The
post-run-end phantom `other1` twin at :83 reproduced deterministically AND
cleared with the owned `remove_breakpoint` (list empty after) — that half is
real, benign, and already handled. Reclassify: keep open for the PollResume
race only if it recurs; the twin needs no fix.

**Triage (watch canary, no code):** 1 stop in 2 runs with identical binary —
timing flake, not deterministic. The phantom post-run-end twin is
deterministic but benign (clears with the owned `remove_breakpoint`, already
handled). Next full E2E decides it: another C10 launch-stop → full
PollResume-race investigation; clean pass → close. Until then this stays open
as the canary.

**Status:** needs-info

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R1R.md` rows C10,
C10-supplement. Re-run C10 alone to check flakiness before root-causing.
