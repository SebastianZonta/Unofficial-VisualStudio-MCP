# Run to Cursor

Status: ready-for-human

## Problem Statement

Step-over stepping is mile-by-mile: reaching a line a hundred statements ahead takes dozens of agent round-trips, each one a chance to drift past the point of interest or land in library code. Agents need one call to run the debugged target process forward to a given file and line and stop there, without hand-placing a breakpoint, resuming, and remembering to clean it up.

## Solution

A session-scoped `run_to_cursor` execution tool that runs to a given file/line in one call, implemented as a temporary one-shot breakpoint plus resume: the breakpoint is placed, the target runs, and the temporary breakpoint is removed when hit or when the timeout expires. It requires break or run mode and returns the same mode/lastHit envelope as `resume`.

## User Stories

1. As an agent, I want to run to a given file and line in one call from break mode, so that I skip mile-by-mile stepping to reach the code I care about.
2. As an agent, I want to run to a given file and line from run mode as well, so that I can set a forward destination without pausing first.
3. As an agent, I want the temporary breakpoint removed automatically when hit, so that my breakpoint list is not polluted with one-shot stops.
4. As an agent, I want the temporary breakpoint removed when the timeout expires without hitting it, so that a missed destination never leaves a stray breakpoint behind.
5. As an agent, I want the result in the same mode/lastHit envelope as `resume`, so that all execution continuations read identically.
6. As an agent, I want lastHit to report the destination file and line when the run stops there, so that I can confirm arrival without a second call.
7. As an agent, I want a timeout result that reports still-running mode with the temporary breakpoint already cleaned up, so that I know the destination was not reached and nothing lingers.
8. As an agent, I want a clear error for a file/line the debugger cannot bind, so that a typo fails fast instead of running to the timeout.
9. As an agent, I want the run scoped to my debug session's target process, so that sibling sessions are never resumed or disturbed.
10. As an agent, I want my pre-existing breakpoints to survive the call untouched, so that run-to-cursor never deletes breakpoints I placed myself.
11. As an agent, I want a concurrent breakpoint hit on the way to the destination to stop the run and still clean up the temporary breakpoint, so that an earlier hit is never swallowed.
12. As an agent, I want a mode error when the debugger is in design mode, so that run-to-cursor cannot fire before the target is launched.
13. As a builder, I want run-to-cursor behind the single bridge seam as a composed place-resume-remove operation, so that tools stay thin and the one-shot lifecycle lives in one place.

## Implementation Decisions

- New bridge-seam operation `RunToCursor` plus a matching MCP tool beside `resume` and `step`; no behavior change to `resume`, `step`, or breakpoint tools. The `IDebugBridge` seam is the only boundary crossed; the tool stays thin and delegates through it, with `FakeDebugBridge` as the in-memory test double.
- One-shot lifecycle contract: place temporary breakpoint at file/line, resume, on stop (destination hit, other breakpoint hit, or timeout) remove the temporary breakpoint before returning. Removal is unconditional — every exit path cleans up.
- Destination contract: file plus one-based line, validated and bound exactly like the existing set-breakpoint contract (same path normalization, same unbindable-location error). No new addressing scheme is introduced.
- Mode contract: accepted in break or run mode; rejected in design mode with the existing mode error. Return envelope matches `resume`: mode plus lastHit (destination file:line when stopped there, null when still running at timeout).
- Timeout semantics mirror `resume`: the caller-supplied timeout bounds the run; expiry stops waiting, guarantees cleanup, and reports the still-running envelope rather than an error.
- Pre-existing breakpoints are never touched: only the temporary breakpoint created by the call is removed; a concurrent hit on a user breakpoint returns that stop with lastHit pointing at it.
- Glossary vocabulary governs naming: debug session versus target process versus VS instance; mode and lastHit keep their existing meanings; the temporary breakpoint is never surfaced in breakpoint listings as a persistent entry.
- Live COM mapping implements the lifecycle as place-breakpoint plus resume plus delete-breakpoint against EnvDTE, snapshotted to plain DTOs; live handles never escape; COM errors map through the existing error mapper.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, the one-shot lifecycle (placed then removed), timeout cleanup, and mode gating. No assertions on EnvDTE internals, call order, or private state.
- Tested surface: run-to-cursor at the tool-call boundary against `FakeDebugBridge` (arrival reports destination lastHit, temporary breakpoint gone afterwards); timeout path (still-running envelope, no lingering temporary breakpoint); unbindable file/line error; design-mode rejection; pre-existing breakpoints surviving the call; concurrent-hit path returning the earlier stop with cleanup done.
- Prior art: tool-boundary tests against the fake bridge in the style of the existing execution-control tests; fake-seeded break/run/design fixtures without touching a real debugger, run with `dotnet test VsDebugMcp.slnx` (no VS needed).
- Live coverage stays manual: break in a target, run to a later line in the same function, verify one call arrives with lastHit at the destination and the breakpoint list unchanged.

## Out of Scope

- Set-next-statement (moving the instruction pointer without executing): named here only as a follow-up candidate, not designed in this spec.
- Step-back / reverse debugging or any backward movement.
- Conditional or counted run-to (predicates on arrival); the destination is file/line only.
- Multi-cursor or fan-out runs across sessions; one call addresses one session and one destination.
- Persisting the temporary breakpoint anywhere; it exists only for the duration of the call.
- Any change to `resume` / `step` / breakpoint-tool semantics or to the mine/consent attach gates.

## Further Notes

- Running forward executes code: breakpoints and side effects between the start and the destination fire normally, which is why a concurrent earlier hit legitimately wins.
- If the destination line holds no executable code, the debugger binds the nearest bindable location per its own rules and lastHit reports where it actually stopped.
