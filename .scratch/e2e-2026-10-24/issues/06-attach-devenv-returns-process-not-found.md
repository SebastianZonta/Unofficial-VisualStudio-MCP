# 06: `attach_process` on a devenv PID returns `process-not-found`, not `invalid-argument`

**What broke (E2E 2026-10-24-1800-R1, E26):** `attach_process(31600,
consent:true)` where 31600 is R1's own VS (`devenv`) → `process-not-found: no
local process 31600`, expected `invalid-argument` naming devenv (self-attach
refusal). The PID was valid: unfiltered `list_processes` contained
`{pid:31600, name:devenv, mine:true}` and `list_instances` showed it reachable
before and after.

**Suspected:** the attach lookup filters devenv out (cf. E14's
`invalid-argument: devenv is a VS instance` for `launch_process`) before the
self-attach guard runs, so the guard never sees it. AGENTS.md says
`attach_process` "refuses them with `invalid-argument` (never a self-attach)".

**Blocked by:** None.

**Fix (done, unit-green, needs live E26):** root cause was not a missing
guard — both bridges have it. EnvDTE `LocalProcesses` omits devenv itself,
so the live lookup missed the PID and threw `process-not-found` before the
guard (the fake's seeded list includes devenv, so unit tests stayed green —
exactly the fake/live divergence from the retro). Fix:
`DteDebugBridge.IsDevenvPid` (internal static, OS process-name check,
never throws) consulted on lookup miss; same `invalid-argument` message as
the fake. New test pins the no-throw shape; positive case is E26 live.
309/309 unit green. Published to `standalone-v7/` — needs host restart +
scoped live E26 (`attach_process(<devenv pid>)` → `invalid-argument`) to
close.

**Status:** needs-triage

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R1.md` row E26.
