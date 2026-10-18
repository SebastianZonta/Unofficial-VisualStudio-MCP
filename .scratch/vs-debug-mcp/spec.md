Status: ready-for-agent

## Problem Statement

An AI agent driving development inside Visual Studio 2026 cannot see or control the C# debugger: it cannot set a breakpoint, resume to it, step, read the stack and variables, or evaluate an expression. Today the only programmatic routes (hand-rolled DTE scripts, a VSIX, engine-level APIs) all demand Visual Studio expertise the agent does not have, so agents either debug blind (edit-and-pray) or hand control back to the human for every runtime question.

## Solution

A same-machine, C#-only, minimal-core debugger MCP server for Visual Studio 2026 that any agent can use without reading VS docs: ten plain-named stdio tools with safe defaults, backed by an out-of-proc COM/DTE bridge requiring no extension install. The agent attaches to the running VS instance, sets breakpoints, pauses, resumes, steps, reads stacks and variables, and evaluates read-only expressions — every result carrying debugger mode plus last-hit, every value truncated with drill-down, every failure a named error with remediation.

## User Stories

1. As an agent, I want to attach to the running VS 2026 instance and get a session id, so that all later calls are scoped without re-resolving the IDE.
2. As an agent, I want an explicit instance override on attach, so that multi-instance machines bind deterministically instead of newest-wins surprising me.
3. As an agent, I want a clear error when no VS instance or solution is available, so that I stop and report instead of hanging.
4. As an agent, I want to launch the open solution (F5-equivalent) through the debugger, so that I can start a session without touching the IDE.
5. As an agent, I want to attach to a local process by id with an explicit consent flag, so that arbitrary-process attach is deliberate and auditable.
6. As an agent, I want F5-launch of the open solution to need no extra consent, so that the common loop stays frictionless.
7. As an agent, I want a new attach to replace the old binding with a stale-session error on the old id, so that single-session semantics are never ambiguous.
8. As an agent, I want to set a file/line breakpoint and get its id plus bind state immediately, so that I never block waiting for symbols.
9. As an agent, I want pending (unbound) breakpoints reported with a warning rather than an error, so that I can poll the list until they bind.
10. As an agent, I want to list breakpoints with bind states, so that I can verify arming before resuming.
11. As an agent, I want to remove a breakpoint by id, so that I can clean up probes I no longer need.
12. As an agent, I want to pause a running debuggee, so that I can inspect on demand.
13. As an agent, I want to resume execution, so that the debuggee runs to the next breakpoint or timeout.
14. As an agent, I want one step tool with a direction parameter (over, into, out), so that I learn one tool instead of three.
15. As an agent, I want every execution result to carry debugger mode and last-hit breakpoint, so that I never need a separate status call.
16. As an agent, I want a timeout status (not a hang) when a step or resume exceeds its budget, so that I can decide to wait, retry, or pause.
17. As an agent, I want to read the call stack capped at a small default frame count, so that deep stacks never flood my context.
18. As an agent, I want to read a frame's locals and arguments through one variables tool with an expandable path, so that locals, args, and nested members are a single drill-down motion.
19. As an agent, I want values truncated with an expandable flag, so that large collections never dump into my context window.
20. As an agent, I want a read-only expression evaluator, so that I can test hypotheses about state without mutating the debuggee.
21. As an agent, I want value-assignment and statement-execution to be absent (not merely discouraged), so that I cannot mutate state by accident.
22. As an agent, I want inspect operations outside break mode to return a not-in-break-mode error, so that I get guidance instead of a COM dump.
23. As an agent, I want elevation-mismatch and VS-busy failures as named errors with remediation text, so that I can tell the human exactly what to fix.
24. As a builder, I want the tool surface enumerable from explicit registration (no assembly-scan creep), so that the ten-tool budget is reviewable and enforced.
25. As a builder, I want all automation behind one bridge interface with deterministic fakes, so that the full tool surface is testable without a running VS.
26. As a builder, I want a scripted core-loop walkthrough against a sample C# app on real VS 2026, so that I can prove the loop before calling the spec done.

## Implementation Decisions

- **Stack**: C# console host on the official MCP C# SDK (`ModelContextProtocol` v2.x) plus host/di packages, targeting net8.0 or newer; stdio transport only — no HTTP listener, no ports, no auth surface.
- **Registration**: attribute-based tool discovery with descriptions on every tool and every parameter (descriptions are the agent's UI); explicit per-group registration so the surface stays grep-reviewable; no assembly-scan registration, no dynamic tool factory, no prompts or resources in v1 — tools only.
- **Module shape**: one tool group per concern (session, breakpoints, execution control, inspection); all tools take an opaque session id scoping server-side state; framework-injected values (cancellation token, server handles, injected services) never appear in the tool schema; validation is explicit in-method since schema attributes only shape the schema.
- **The one new seam**: a single bridge interface between the ten tools and the debugger automation, with plain-data DTOs crossing it (breakpoint with id, file, line, enabled, bound plus optional warning; frame with id, function, module, optional file and line; value with name, type, truncated string, expandable flag plus drill-down path). The bridge snapshots live COM objects to DTOs on every call and never hands live handles to tools; frame and thread ids are session-scoped tokens re-fetched after any execution-control call.
- **Automation API**: EnvDTE debugger family (`Debugger2`/`Debugger3`/`Debugger5`, `Breakpoint2`, `Expression`, `StackFrame`, thread) reached via the DTE object of the discovered instance; Concord engine APIs and legacy engine-implementor interfaces are out — the shipped managed engine already implements them and the bridge only observes results through automation.
- **Instance discovery**: enumerate the running-object table, bind the newest VS 2026 instance, accept an explicit override, fail cleanly when absent; never hardcode a versioned program id; bridge and VS must run at the same integrity level (documented, named error otherwise).
- **Execution control**: launch (F5-equivalent), pause, resume, and single step tool with over/into/out direction; every blocking call uses the synchronous-wait form wrapped in the tool's timeout plus cancellation token; timeout is a first-class result status; single dispatcher thread serializes all automation calls — parallel agent requests queue, never concurrent COM.
- **State gating**: mode is checked before any inspect or evaluate call; anything outside break mode returns the named error, and every successful result echoes mode plus last-hit breakpoint.
- **Breakpoints**: set, remove, and list only — no enable/disable toggle, no conditions, no hit counts in v1; bind state is reported (bound flag plus warning) with immediate return and poll-to-confirm semantics.
- **Inspection**: one stack tool (capped frames) plus one variables tool (frame id plus optional expandable path unifying locals, arguments, and member drill-down); one read-only evaluator with explicit finite timeout; statement execution and value assignment are separate, deferred tools — absent from v1.
- **Defaults**: waiting calls default to 5000 ms; stack reads default to 20 frames; variable reads default to 50 entries; side effects default off everywhere (evaluate is read-intent; attach to arbitrary processes needs explicit consent and same-user).
- **Errors**: expected failures (bad session, no symbols, timeout, break-mode violation, elevation, busy) are tool results carrying a machine-readable code plus human remediation; unexpected failures stay generic with no leaking internals; protocol and cancellation errors keep their reserved meanings and are never reused for domain failures.
- **Logging**: every log line to standard error; standard output is the protocol pipe and nothing else may write to it.
- **Result envelope** (validated by the throwaway stub-prototype walkthrough; trimmed to the decision): every tool result carries debugger mode and last-hit alongside its payload; every collection or value carries truncation plus an expandable drill-down path; every failure carries a code plus remediation. The prototype's verdict (pass, no changes) is the evidence this envelope meets the simplicity bar.

## Testing Decisions

- **What makes a good test**: drive the public boundary (scripted tool calls in, result envelopes out) and assert external behavior only — payload shapes, caps, named error codes, mode/last-hit presence — never the internals of the automation calls.
- **Which modules**: the ten tools through the stdio boundary against a fake bridge (all happy paths plus bind-warning, timeout, not-in-break-mode, no-session, elevation, and busy paths); the bridge interface with mocked automation for discovery, gating, snapshot, and serialization rules; the real EnvDTE wiring is covered only by the acceptance walkthrough, not by unit tests.
- **Prior art**: the VsBridge project (same topology — local exe over COM/EnvDTE, similar tool count, mocked-COM tests) is the reference for both the mock style and the failure-mode list; the throwaway stub-prototype's four walkthroughs (happy path, unbound breakpoint, break-mode violation, timeout) are the scenario seeds for the scripted tool tests.
- **Acceptance**: scripted core loop against a sample C# console app on real VS 2026 — attach, set breakpoint, resume, step, read stack, read variables, read-only evaluate — plus the bind-warning poll path and each named error path at least once by the builder before handoff.

## Out of Scope

- VS Code, Rider, or any non-VS-2026 IDE; remote debugging across machines; any language beyond C#.
- Conditional breakpoints, watch windows, threads/tasks parallel views, Edit-and-Continue, profiler/IntelliTrace, exception-settings management, minidumps.
- Value assignment and statement execution (deferred as separate consent-gated tools); multi-process or multi-target operations (named-limitation errors instead).
- HTTP transport, authentication, prompts/resources, assembly-scan tool registration, dynamic tool generation.
- Custom debug-engine authorship (Concord/MIEngine) and any in-proc VSIX — the VSIX remains only a documented fallback if build-time validation hits unresolvable targeting, hang, event, or elevation walls.

## Further Notes

- Topology precedent: a same-machine local-exe-over-COM bridge with roughly ten tools and the same failure modes already exists and claims VS 2026 support — the spec's job is the simplified, agent-proofed surface on top of that proven shape, not proving the topology.
- Open verification items for the builder (from research): ROT program-id for the 2026 release (enumerate, do not hardcode); Breakpoint2 bind-reporting fidelity for C# on Core versus Framework targets; newer-C# syntax skew in the shipped expression evaluator; break-event latency (poll versus event sink) informing timeout defaults; re-check of the modern extensibility SDK per 18.x minor for any debugger-automation additions.
- The full decision trail (research with sources, grill verdicts, stub verdict) lives on the wayfinding map and its six closed child issues; the stub-prototype file is throwaway evidence and must never be merged or built upon.
