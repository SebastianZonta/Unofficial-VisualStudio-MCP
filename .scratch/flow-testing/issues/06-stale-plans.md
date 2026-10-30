# 06: Stale plans

**What to build:** When a checkpoint's anchor no longer matches the code, the flow runner sets no breakpoint for it and the run is BLOCKED with "plan stale" naming the checkpoint; the return carries a stale-plan flag. The main agent offers to send the planner back to re-anchor that plan, and does so only on the developer's yes; re-anchoring that would change an expectation comes back as a question, never a silent rewrite. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** ready-for-agent

- [ ] Edit an anchor fragment away in the Sandbox copy → BLOCKED "plan stale" naming the checkpoint
- [ ] Moved-but-intact anchor (lines shifted) still resolves and runs
- [ ] Main agent offers re-anchoring and waits for the developer
- [ ] On yes, the planner updates anchors only; any expectation change is returned as a question
- [ ] Rerun after re-anchoring reaches a verdict
