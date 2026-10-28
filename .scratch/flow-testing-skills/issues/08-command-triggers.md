# 08: Command triggers

**What to build:** A developer plans and runs a use case triggered by an HTTP call.
Against the Sandbox `serve` scenario it passes end to end:

- setup records trigger defaults (base URL, readiness check, token env var *names*)
  and dependencies, each with a readiness check;
- the planner writes command triggers and outcome checks;
- the run launches the target once per flow run and reuses it across use cases, waits
  for readiness, starts command triggers in the background per ticket 07's P3 answer,
  checks the outcome after driving the use case, and stops what it launched;
- a dependency that's down blocks the use cases needing it, naming the service;
- redacted checkpoints arrive here.

Spec: [Setup](../spec.md#setup-vs-debug-setup), [Run](../spec.md#run-vs-debug-run-flow-adr-0013),
[Safety](../spec.md#safety).

**Blocked by:** 03 (Plan a launch-triggered flow), 07 (Sandbox `serve` scenario and background-trigger probe)

**Status:** ready-for-agent

- [ ] Setup records base URL, readiness check, token env var names (never values) and dependencies with readiness checks; the smoke check passes each host's readiness check.
- [ ] Plans express `command` triggers and outcome checks (status code, exit code, stdout).
- [ ] A target needed by command triggers is launched once per flow run and reused; a `launch` trigger stops it first and starts fresh; the run stops only processes it launched.
- [ ] A command trigger runs in the background; a pause at a checkpoint never shows as a failed request; the outcome check reads the collected output.
- [ ] A dependency that's down is BLOCKED for the use cases needing it, naming the service; the run never starts it.
- [ ] A redacted checkpoint shows only match or mismatch in the verdict; no plan or setup doc holds a secret value.
- [ ] Skills pass cases: an HTTP use case on `serve` gives PASS; a stopped dependency gives BLOCKED with its name.
