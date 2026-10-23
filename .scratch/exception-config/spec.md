# exception-config (exception-break configuration)

Status: ready-for-agent

## Problem Statement

Today exceptions surface only after the fact as `[Exception]` frames with no `lastHit`: the agent sees that something threw, but cannot ask the debugger to stop at first chance for a specific exception type (e.g. `System.NullReferenceException`) ahead of time, nor inspect what the current exception-break settings even are. Tuning requires clicking through the VS Exception Settings UI by hand.

## Solution

Add session-scoped tooling to configure first-chance versus unhandled-only breaking per exception type plus a listing of current settings. When an exception break fires, the result carries the exception object path so `read_variables` / `evaluate` (the `$exception.Message` pattern) work immediately. Reading settings is allowed in any mode; changing settings requires break or design mode, never while running.

## User Stories

1. As an agent, I want to list current exception-break settings in my debug session, so that I know what will stop the target process before I run it.
2. As an agent, I want to list settings in any mode including while running, so that read-only inspection never disturbs execution.
3. As an agent, I want to enable first-chance breaking for one exception type by name, so that the debugger stops the moment it is thrown.
4. As an agent, I want to restrict one exception type to unhandled-only breaking, so that caught-and-handled throws stop interrupting my session.
5. As an agent, I want to reset one exception type to its default, so that exploratory tuning is reversible.
6. As an agent, I want changes rejected while the target process is running, so that settings edits happen only in break or design mode.
7. As an agent, I want a change attempted while running to name the current mode in the error, so that I know to pause or detach-to-design first.
8. As an agent, I want an unknown exception type name to be rejected with a not-found error, so that a typo never silently configures nothing.
9. As an agent, I want exception settings scoped to my own debug session, so that tuning never leaks into a sibling debug session on another VS instance.
10. As an agent, I want an exception break to carry the exception object path in its result, so that `read_variables` / `evaluate` work immediately without re-discovery.
11. As an agent, I want the `$exception.Message` style probe to work right after the break, so that I can triage the throw without extra round trips.
12. As an agent, I want the break result to echo the mode plus the exception type name, so that the transcript shows exactly what stopped execution.
13. As an agent, I want `[Exception]` frames without `lastHit` to remain the after-the-fact view, so that existing stack reading keeps working alongside the new pre-configuration.
14. As a builder, I want exception settings verified without a live VS instance, so that configuration logic is testable in CI.

## Implementation Decisions

- New session-scoped exception-settings operations on the IDebugBridge seam plus thin MCP tools beside the existing execution tools; tools stay thin and all debugger access crosses the seam.
- Settings contract: per exception type name (e.g. fully qualified CLR name), a breaking mode of first-chance versus unhandled-only versus default; listing returns the full current set with each entry's mode.
- Change operations take session id, exception type name, and desired breaking mode (or reset-to-default); the bridge validates the type name and rejects unknown names before touching debugger state.
- Mode gate: listing is allowed in any session mode (design, run, break) and never mutates state; changes require break or design mode and are rejected while running, consistent with the fail-closed execution-control posture.
- Break payload: when an exception break fires, the execution result carries the exception object path in the same path language `read_variables` consumes, so the existing `$exception`-style probes resolve immediately.
- Session isolation: settings and break payloads resolve within the calling debug session only; one session's tuning never affects a sibling debug session or a different VS instance.
- Glossary vocabulary governs naming and results: debug session versus VS instance versus target process, mode versus lastHit (exception frames still carry no `lastHit`), and session-less stays reserved for pure discovery tools.
- Existing `[Exception]` frame rendering and `read_stack` semantics are unchanged; this spec adds pre-configuration plus the break-time object path, not a new stack format.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and error shapes. No assertions on COM internals, call order, or private state.
- Tested surface: list returns seeded settings in any mode; enable-first-chance and restrict-to-unhandled-only round-trip through list; reset restores default; change-while-running rejected with mode named; unknown type name rejected; exception-break payload carries a usable object path; cross-session isolation.
- Prior art: FakeDebugBridge as the in-memory test double, in the style of the existing execution-control and inspection tool-boundary tests — seed fake exception settings and breaks, drive the tool boundary, assert envelopes and state, no VS needed.
- Live coverage stays manual: configure first-chance for a test exception type, run the target process, confirm the break carries the object path, probe the message, reset to default.

## Out of Scope

- Global or persisted Exception Settings: configuration is per debug session and does not rewrite the VS-wide defaults file.
- Group toggles (whole CLR category on/off) beyond per-type settings; bulk operations are repeated single-type calls.
- Exception filters by module, condition expressions, or break-when-thrown counts.
- Custom exception visualizations or new stack frame formats.
- Session-less exception configuration; a debug session is always required.
- Changing settings while running (queued-apply on next break is explicitly not supported).

## Further Notes

- Type names should be fully qualified CLR names; short names risk collisions across assemblies and should be rejected or resolved deterministically by the bridge.
- Agents should prefer unhandled-only for noisy framework exceptions and reserve first-chance for the one or two types under investigation, to avoid break storms.
