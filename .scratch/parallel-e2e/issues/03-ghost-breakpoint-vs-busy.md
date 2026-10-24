# 03: `vs-busy` on `set_breakpoint` can orphan an unremovable ghost breakpoint

**What happened (pilot 2026-10-24-1359, R3, E5 take 1):** `set_breakpoint` at
HEARTBEAT threw `vs-busy` (HRESULT 0x80010001), but VS had actually created the
breakpoint. The retry returned `breakpoint-exists` naming it `other1` (not
created by this debug session) — list-only, unremovable via tools, and it would
have broken E6 (resume re-breaks instead of running). Recovery within slice:
stopped the target, detached, killed VS, deleted the copy's `.vs`
(suo persistence), restarted, re-attached clean, re-ran P8 from E1 — all cases
then passed cleanly.

**Hypothesis:** the bridge loses ownership when `Breakpoints.Add` succeeds but
a later COM call in the same operation fails under load. One occurrence, under
three-runner contention; R1 saw a similar transient `other1` twin that
self-cleaned on remove (no action needed there).

**Status:** needs-triage

- [ ] Maintainer: evaluate. If it recurs, candidates are (a) retry
      `set_breakpoint` idempotently on `vs-busy` instead of failing open, or
      (b) re-resolve ownership after a busy window. Single data point — watch
      item, not a blocker.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1359-R3.md` OBS-E5-ghost;
`.scratch/e2e-runs/2026-10-24-1359-R1.md` OBS-twin.
