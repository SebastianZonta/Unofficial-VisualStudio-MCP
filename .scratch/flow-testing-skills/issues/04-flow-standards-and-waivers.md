# 04: Flow standards and waivers

**What to build:** A developer states once, during setup, what must hold for every flow
("no unhandled exception", "no first-chance `SqlException`", "never reaches
`Debug.Fail`"). Every flow run then checks those standards in every use case, and a use
case can waive one with a reason. Specifically:

- setup interviews the developer and keeps only checkable standards in the flow
  standards file, linked from the setup doc;
- plans reference the standards (never copy them) and may carry named waivers;
- the planner offers to move rules that apply to every flow into the standards;
- runs gain exception checks;
- the verdict quotes the standards it checked and lists every waiver.

Spec: [Setup](../spec.md#setup-vs-debug-setup), [Run](../spec.md#run-vs-debug-run-flow-adr-0013),
GLOSSARY "Flow standards", "Waiver".

**Blocked by:** 03 (Plan a launch-triggered flow)

**Status:** ready-for-agent

- [ ] Setup writes the flow standards file and links it from the setup doc. A vague criterion is rewritten with the developer into a checkable form or left out, and timing standards are refused.
- [ ] Plans point to the flow standards; no standard is copied into a plan.
- [ ] A use case can waive a named standard with a one-line reason; a waiver naming an unknown standard is rejected at plan time.
- [ ] The planner offers to move a rule the developer states for every flow into flow standards.
- [ ] Exception checks: the run sets first-chance breaking for the named type before launch and resets every changed exception setting on every exit path (ADR-0011).
- [ ] An exception nobody expected is FAIL, naming the standard it breaks, unless waived.
- [ ] The verdict quotes every standard checked and lists every waiver per use case.
- [ ] Skills pass cases: a waived standard appears in the verdict; a broken standard is FAIL naming it; exception settings are back to their prior state afterwards.
