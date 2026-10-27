# 03: E28 manual run-to-cursor window + phase H (needs a person at the keyboard)

**Status:** ready-for-human

**What (E2E 2026-10-26-2211-R3, E28 BLOCKED):**
E28 needs a person setting a breakpoint in VS mid-`run_to_cursor` List–Set
window. R3 correctly did not attempt it (runner isolation: act on VS only
through MCP tools). Not a product failure.

**Do (maintainer, with the user at the keyboard):**
Run phase H per `docs/agents/e2e-phases-closeout.md` (H1–H5) plus E28, and
append the results to `.scratch/e2e-runs/2026-10-26-2211.md`:
start VS A as in S2/S3, `attach(A)` → SH, ask before each manual step.
Then set `Status: resolved` here with the evidence table.

**Also noted, no action:**
- R3's first debug session vanished mid-run (`no-session`; host binary
  re-timestamped 03:53 UTC — the host restarted under the run). Re-attached,
  no case lost. Infra noise, not a product FAIL — but do not restart the
  host mid-pass again.
- Repo HEAD moved 86c9528 → 44933ea docs-only mid-run; binary predates it,
  no src change, scope unaffected.

**Blocked by:** None (waits on a human window, not on other tickets).

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-26-2211-R3.md` rows E28, O-session, O-head.
