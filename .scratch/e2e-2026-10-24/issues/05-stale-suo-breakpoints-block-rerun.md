# 05: Stale suo breakpoints in repo Sandbox block B3/B5/B7/C10/Z1 re-run

**What broke (E2E 2026-10-24-1800-R1):** two foreign breakpoints (`other1` @
`Sandbox/Scenarios.cs:83` WORKER, `other2` @ `:114` ECHO, both `bound:true`)
pre-date the run (present in the first listing before any launch) and persist
in `Sandbox/.vs/` suo state — almost certainly orphaned by pre-fix runs (see
`01`, fixed since). Every session-owned assertion held (distinct ids, own
removals echo, detached session's bp gone with no twin); only exact-listing
expectations fail because MCP correctly cannot remove foreign breakpoints
(`breakpoint-not-found`, "remove it in VS"). C10 is BLOCKED: the stale `other1`
sits on its exact target line and `set_breakpoint` correctly refuses with
`breakpoint-exists`.

**Fix:** no code change. With VS closed, delete `Sandbox/.vs/`, or open the
repo `Sandbox.slnx` in VS and delete the breakpoints at Scenarios.cs:83/114 by
hand; then re-run B3/B5/B7/C10/Z1. Also delete the locked empty remnant
`%TEMP%\e2e-2026-10-24-1800-R2B` (R2's VS B held a handle when killed).

**Blocked by:** None.

**Status:** resolved

R1R re-run (`.scratch/e2e-runs/2026-10-24-1800-R1R.md`) on deleted `.vs`:
B3/B5/B7/Z1 all PASS with zero `other<N>`. Cleanup confirmed; no code change
was needed for these five.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R1.md` rows B3/B5/B7/C10/Z1.
