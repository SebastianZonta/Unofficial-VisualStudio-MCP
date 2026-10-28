# 11: Human triggers and human-gated runs (P4)

**What to build:** A developer whose flow needs a person in the middle (approve the
order in the admin UI) gets it checked. At a human trigger the run:

1. makes sure no target process is stopped;
2. arms the next checkpoints;
3. prints the instruction filled with captured values;
4. takes "done" as the next armed checkpoint hitting or the developer saying done.

Attach consent is asked once per flow run, naming the process and target PID, and is
never stored. When an agent starts a run on its own, it does every ungated use case
first, then asks before each human-gated one.

This ticket also answers **P4**: does text the agent prints before a blocking `resume`
reach the human while the call is pending? If not, the run uses the fallback (end the
turn after printing, wait for "done", then resume the wait).
Spec: [Run](../spec.md#run-vs-debug-run-flow-adr-0013), [Safety](../spec.md#safety),
[Capabilities and probes](../spec.md#capabilities-and-probes) P4; GLOSSARY "Human-gated".

**Blocked by:** 09 (Deferred work: budgets, captures, kicks)

**Status:** ready-for-agent

- [ ] Plans express `human` triggers anywhere in a use case; the planner marks use cases human-gated (a human trigger or attach consent).
- [ ] At a human trigger, all of the steps above happen in that order; the next checkpoint's deferred budget starts when the human trigger is done.
- [ ] Attach consent: a use case needing `attach_process` gets one prompt per flow run naming the process and target PID; consent is never written to a plan or the setup doc.
- [ ] Agent-started runs do every ungated use case first, then ask before each human-gated one; runs the developer started keep plan order.
- [ ] P4 answered per client tried, recorded in this feature's research folder and the spec's probe table; the run applies the fallback where text isn't shown mid-call.
- [ ] Phase H gains a human-trigger case on the Sandbox `serve` scenario (a mid-use-case human step between two checkpoints): PASS with a real person.
