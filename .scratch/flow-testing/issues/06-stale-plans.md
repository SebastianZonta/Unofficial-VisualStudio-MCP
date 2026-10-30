# 06: Stale plans

**What to build:** When a checkpoint's anchor no longer matches the code, the flow runner sets no breakpoint for it and the run is BLOCKED with "plan stale" naming the checkpoint; the return carries a stale-plan flag. The main agent offers to send the planner back to re-anchor that plan, and does so only on the developer's yes; re-anchoring that would change an expectation comes back as a question, never a silent rewrite. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** done

- [x] Edit an anchor fragment away in the Sandbox copy → BLOCKED "plan stale" naming the checkpoint
- [x] Moved-but-intact anchor (lines shifted) still resolves and runs
- [x] Main agent offers re-anchoring and waits for the developer
- [x] On yes, the planner updates anchors only; any expectation change is returned as a question
- [x] Rerun after re-anchoring reaches a verdict

## Comments

Dogfood (2026-10-30, Sandbox copy, `loop-sum`): three lines inserted above `Tick` → anchor resolved at 23 (hint 20), PASS. Renamed `next` to `sum` in `Tick` (`return next;` still exists in `Heartbeat`) → `Stale: yes (checkpoints 1)`, every row BLOCKED, no breakpoint set. Developer said yes; planner changed only `Anchor`/`Line hint`, asked about `next` = `15` (marked proposed until answered); answer `sum` = `15` confirmed it; rerun PASS.
