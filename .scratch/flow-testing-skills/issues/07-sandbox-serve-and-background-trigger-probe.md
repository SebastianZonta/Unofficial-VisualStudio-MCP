# 07: Sandbox `serve` scenario and background-trigger probe (P3)

**What to build:** The Sandbox gains what flow testing needs to exercise command
triggers, decoupled work and out-of-process legs:

- a `serve` scenario: a localhost HTTP endpoint on a listener that needs no URL
  reservation from a non-elevated shell;
- an in-process background loop that handles the work the endpoint queues, carrying the
  queued item's id;
- a second Sandbox process that consumes the same work out of process.

Each piece gets `// E2E:<NAME>` markers, added to the E2E marker table. This ticket
also answers **P3**: does an HTTP request started in the background from the agent's
shell stay pending while the target sits at a breakpoint, then complete after `resume`,
with its output collected from a file?
Spec: [Testing Decisions](../spec.md#testing-decisions) (Sandbox additions),
[Capabilities and probes](../spec.md#capabilities-and-probes) P3.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `serve` answers an HTTP request on localhost without URL reservation or elevation; the listener choice is recorded with its reason.
- [ ] A request queues work that the in-process background loop handles after the response has been sent, carrying the request's id.
- [ ] The second Sandbox process consumes the same work out of process, carrying the same id.
- [ ] Every new breakpoint target has a unique `// E2E:<NAME>` marker on an executable statement; the E2E marker table lists them; existing scenarios and markers are unchanged, and the existing E2E cases still pass on the scenarios they use.
- [ ] P3 answered with evidence from the MCP tools: a background request issued while a breakpoint holds the handler stays pending, completes after `resume`, and its output is read from a file. The result is recorded in this feature's research folder and the spec's probe table.
- [ ] If P3 fails, the note names the mechanism that works instead, and the spec's command-trigger promise is updated to match.
