# 04: Two-VS pass blocked — second VS never automation-ready (environment)

**What happened (E2E 2026-10-23-2217):** M0 — 5 launches of a second VS (3× `%TEMP%\SandboxB` incl. one with `/Log`, 1× Desktop copy `SandboxBDiag`) never became automation-ready: `reachable: false, error: "automation-failed: solution unreadable"` after 6+ min each, idle with an extra untitled visible window (suspected undismissable prompt; runner rules forbid interaction). Persists with instance A stopped, so it is not Temp-trust or instance rivalry. Original-path instances load fine twice (19432, 13932). M1–M9 blocked as a consequence.

**Triage question (human):** retry the M-series on this or another machine with the prompt dismissed, or capture what the second instance is stuck on (ActivityLog from the `/Log` run was cleaned in Z3 — reproduce and keep it).

**Blocked by:** None (environmental, not code).

**Status:** needs-info

- [ ] Second VS reaches `reachable: true` on a Sandbox copy, or the blocking prompt/cause is identified.
- [ ] M1–M9 re-run green once M0 is unblocked.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-23-2217.md` rows M0–M9. Copies and logs were cleaned in Z3; nothing to inspect — must reproduce.
