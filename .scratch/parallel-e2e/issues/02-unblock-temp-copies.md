# 02: Temp Sandbox copies carry Mark-of-the-Web; VS sits at `solution unreadable`

**What broke (pilot 2026-10-24-1359, R2 + R3):** first VS starts on both temp
copies never became reachable (~3 min `automation-failed: solution unreadable`,
empty solution) while R1's repo-backed VS was fine. R3 diagnosed Zone.Identifier
(Mark of the Web) on every copied file — VS raises a trust prompt. Recovery:
`Unblock-File` on the copy + VS restart (reachable in ~60–80 s). Cost R3 ~7 min.

**Fix (done):** `docs/agents/e2e.md` Roles step 4 now runs `Unblock-File
-Recurse` on both temp copies right after copying, before fan-out.

**Status:** needs-triage

- [x] Pre-step added to `e2e.md`; contract tests green.
- [ ] Maintainer: close after the re-run shows clean first starts.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-24-1359-R3.md` OBS-S2-MOTW,
`.scratch/e2e-runs/2026-10-24-1359-R2.md` S3-note. Residual watch item: all
three first starts raced (cold-start contention) — if clean re-runs still miss
the ~7 min target, the follow-up is staggering P0 starts, never re-merging
slices.
