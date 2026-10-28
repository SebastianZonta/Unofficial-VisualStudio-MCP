# 10: Out-of-process legs across two VS instances

**What to build:** A developer whose flow continues in a separate worker process gets
checkpoints in that process too:

- setup records which open VS instance holds which target process;
- the run binds each VS instance in its own debug session and coordinates them;
- cause across processes is proven by captures, never by order;
- a missing second VS instance blocks only the checkpoints in that process, with the
  instruction to open it.

On the Sandbox, a use case reaching the second Sandbox process in a second VS instance
passes. Spec: [Run](../spec.md#run-vs-debug-run-flow-adr-0013), ADR-0008, ADR-0013;
prior art: E2E P10's two-VS setup.

**Blocked by:** 09 (Deferred work: budgets, captures, kicks)

**Status:** ready-for-agent

- [ ] Setup records the VS instance per target process and its smoke check finds both.
- [ ] Plans name a checkpoint's target process; order checks are limited to one process; cross-process checkpoints correlate through a capture.
- [ ] The run binds each VS instance by solution with `instanceOverride`, one debug session each, and leaves both as found.
- [ ] A missing second VS instance: only that process's checkpoints are BLOCKED, with the instruction to open it; the rest of the use case still runs. The run never starts VS.
- [ ] Skills pass cases: the Sandbox consumer process in a second VS instance gives PASS; with the second VS instance closed, its checkpoints are BLOCKED with the instruction.
