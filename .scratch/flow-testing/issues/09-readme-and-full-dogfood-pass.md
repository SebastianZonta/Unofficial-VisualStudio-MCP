# 09: README section and full dogfood pass

**What to build:** `README.md` gains a short "Flow testing" section introducing the three skills. The full dogfood checklist from the spec's Testing Decisions runs end to end against real VS from an installed copy of the skills, and the results are recorded in this file under `## Comments` with a verdict. See [spec](../spec.md).

**Blocked by:** 02 (Ship skills in release and installer), 05 (command and human triggers, attach targets), 06 (Stale plans), 07 (Area and "all" requests), 08 (Multi-solution: cross-solution flows and parallel runs)

**Status:** ready-for-agent

- [ ] `README.md` "Flow testing" section: what the skills do, setup first, one example request each
- [ ] Skills installed via the installer (not copied by hand) before the pass
- [ ] Every Testing Decisions dogfood step run and recorded with evidence
- [ ] Any FAIL triaged into its own issue under this spec
- [ ] Decide whether the checklist joins `definition-of-done.md`; record the decision here
