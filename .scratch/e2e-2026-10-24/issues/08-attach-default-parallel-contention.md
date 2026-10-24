# 08: No-override `attach()` binds another runner's instance under parallel E2E

**What broke (E2E 2026-10-24-1800-R2, M1):** no-override `attach()` bound
`!VisualStudio.DTE.18.0:31600` (R1's instance, max pid) instead of B (15436).
Stray session detached immediately, no calls under it; M2–M9 green via
explicit `attach(B)`.

**Suspected:** parallel-only artifact of `04`'s fix (same-version tiebreak by
pid descending = newest). Under 3 concurrent runners "newest" is another
runner's instance. Single-runner M1 is unaffected; the e2e doc's isolation rule
("never touch a VS instance you did not start") already forces explicit
`attach(B)` everywhere except M1, so M1 may need a scoping caveat for parallel
runs rather than a code fix.

**Blocked by:** None.

**Disposition (doc caveat, no code):** the contract held — no-override
`attach()` binds the *newest* instance, and R1's instance (max pid) was the
newest on the box. Under 3 concurrent runners "newest" is another runner's
instance by design; no code can prefer "mine" without breaking the
single-runner newest-wins promise from 04. R2 handled it exactly right (stray
session detached immediately, no calls under it, M2–M9 green). `e2e.md` M1 now
says so: in a parallel run, detach immediately and record PASS-with-note.
Single-runner M1 is unchanged.

**Status:** resolved

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1800-R2.md` row M1. Related: `04`.
