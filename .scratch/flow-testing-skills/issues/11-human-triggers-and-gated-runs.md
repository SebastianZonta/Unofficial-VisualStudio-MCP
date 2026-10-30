# 11: Human triggers and human-gated runs

**What to build:** A developer whose flow needs a person in the middle (approve the
order in the admin UI) gets it checked. At a human trigger the run subagent:

1. makes sure no target process is stopped;
2. arms the next checkpoints;
3. stops and returns `human: <instruction>` filled with captured values.

The orchestrator prints the instruction, waits for the developer's "done", and
continues the subagent. The subagent reads `mode`/`lastHit` first (the checkpoint may
already have hit while it was stopped), then carries on.

Attach consent is asked once per flow run, naming the process and target PID, the same
way: the subagent stops and returns `consent: attach <process> PID <pid>`, and attaches
only on the developer's yes. Consent is never stored. When an agent starts a run on its
own, it does every ungated use case first, then the subagent stops before each
human-gated one and the orchestrator asks.
Spec: [Run](../spec.md#run-vs-debug-run-flow-adr-0013), [Orchestration](../spec.md#orchestration),
[Safety](../spec.md#safety); GLOSSARY "Human-gated".

**Blocked by:** 09 (Deferred work: budgets, captures, kicks)

**Status:** ready-for-agent

- [ ] Plans express `human` triggers anywhere in a use case; the planner marks use cases human-gated (a human trigger or attach consent).
- [ ] At a human trigger, all of the steps above happen in that order; the next checkpoint's deferred budget starts when the subagent continues.
- [ ] A developer who acts before saying "done" is still caught: the checkpoint hit is read on continue, not missed.
- [ ] Attach consent: a use case needing `attach_process` gets one prompt per flow run naming the process and target PID, asked by the orchestrator; consent is never written to a plan or the setup doc.
- [ ] Agent-started runs do every ungated use case first, then ask before each human-gated one; runs the developer started keep plan order.
- [ ] Phase H gains a human-trigger case on the Sandbox `serve` scenario (a mid-use-case human step between two checkpoints): PASS with a real person.

## Comments

2026-10-30: P4 dropped. The subagent stops at a human trigger instead of waiting in
`resume`, so nothing has to reach the human mid-call.
