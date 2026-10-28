# 03: Plan a launch-triggered flow

**What to build:** A developer says "test the inspect flow" in plain words, and
`vs-debug-plan-flow` produces a flow plan that ticket 02's run accepts. The planner:

1. reads the code to chart the flow (entry point, members, branches, throw sites, candidate anchors);
2. does a scouting run with logpoints;
3. shows what it saw, labelled observed;
4. asks only about intent;
5. writes the plan in the `Format: 1` template;
6. offers to run it.

On an existing plan it updates in place.
Spec: [Plan](../spec.md#plan-vs-debug-plan-flow), [Flow plan format](../spec.md#flow-plan-format).

**Blocked by:** 02 (Run a hand-written flow plan)

**Status:** ready-for-agent

- [ ] The skill is model-invocable; its description names the trigger phrases ("test the X flow").
- [ ] With no setup doc, it stops with one line pointing to `/vs-debug-setup`.
- [ ] The plan's references block lists the charted members; every checkpoint has an anchor (file, enclosing member, unique snippet), never a line number; no marker comments unless the developer asks.
- [ ] The scouting run shows which candidate anchors hit, in what order and with what values; observed values become expectations only when the developer confirms them.
- [ ] Questions cover only intent: right outcome, triggering input, which use cases matter.
- [ ] The plan is one file per flow with one section per use case, written to the committed plan folder recorded in the setup doc.
- [ ] Re-planning an existing flow refreshes anchors and references, keeps every id, asks only about changed behaviour, and appends new use cases.
- [ ] The planner ends by offering a run; the run works in the same session or a fresh one.
- [ ] Skills pass case: plan the `inspect` flow (scouting included), then run it with ticket 02's run: PASS.
