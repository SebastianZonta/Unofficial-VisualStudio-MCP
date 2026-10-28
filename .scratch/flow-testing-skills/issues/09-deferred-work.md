# 09: Deferred work: budgets, captures, kicks

**What to build:** A developer whose flow continues in background work (a hosted job,
an `IHostedService`, an in-process domain-event handler) gets it checked as part of
the flow. On the Sandbox `serve` background loop, a deferred checkpoint passes:

- it hits after the trigger's outcome, within its wait budget;
- it counts only the hit carrying the id captured earlier in the use case;
- an unrelated hit doesn't count;
- scheduled work can be fired now with a kick.

Spec: [Flow plan format](../spec.md#flow-plan-format), [Run](../spec.md#run-vs-debug-run-flow-adr-0013),
GLOSSARY "Capture", "Kick".

**Blocked by:** 08 (Command triggers)

**Status:** ready-for-agent

- [ ] Plans express deferred checkpoints with a wait budget (setup records the default; a checkpoint may override it) and captures; a deferred checkpoint without a correlating capture states why, or the planner asks.
- [ ] The run reads a capture at its checkpoint and substitutes it as a literal into later conditions.
- [ ] Budgets beyond one `resume` call are consecutive `resume` calls; a deferred checkpoint not hit within its budget is FAIL.
- [ ] A correlated hit count uses a conditional breakpoint that the run counts and resumes.
- [ ] A kick fires scheduled work now; a deferred checkpoint whose work falls outside its budget with no kick is BLOCKED, never left waiting.
- [ ] Skills pass cases: the background loop's deferred checkpoint with a capture gives PASS; a hit for another id is not counted.
