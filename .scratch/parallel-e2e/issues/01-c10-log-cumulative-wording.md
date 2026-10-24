# 01: C10 expects exactly 20 `Log` entries; session `Log` is cumulative

**What broke (pilot 2026-10-24-1359, R1):** C10 FAIL — `Log` totals 40
entries, not 20. The 20 new `bp14` entries are exactly the pairs `1/1`…`4/5`
once each (verified programmatically, never stopped, ends `design`); the other
20 are stale `bp12` entries from C6 persisting across `remove_breakpoint` + a
new `launch_process`.

**Root cause:** doc gap, not regression. `TOOLS.md` defines `Log` as "the
session's buffered logpoint entries" (cumulative by design) and the change
under test was docs-only. C10's expectation ("exactly 20") contradicts the
documented semantic; C6 passes only because it runs first in a fresh session.

**Fix (done):** `docs/agents/e2e.md` C10 now expects "exactly 20 new entries
for this logpoint's id" and notes the session `Log` is cumulative. No product
change.

**Status:** needs-triage

- [x] C10 wording fixed in `e2e.md`; contract tests green.
- [ ] Maintainer: close after the re-run scores C10 PASS.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1359-R1.md` rows C10, OBS-log,
OBS-twin (transient `other1` twin at the same line, self-cleaned — no action).
