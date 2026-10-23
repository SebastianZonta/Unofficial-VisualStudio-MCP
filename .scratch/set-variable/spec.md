# set_variable (gated variable writes)

Status: ready-for-agent

## Problem Statement

`evaluate` is deliberately read-only: assignments and statements are rejected. That keeps ad-hoc expression evaluation safe, but it leaves no way to mutate program state during a debugging session — e.g. forcing a null to non-null, flipping a flag, or injecting a fault to test an error path. Agents must restart the target process with different inputs instead of writing a variable in place.

## Solution

Add a session-scoped `set_variable` tool that writes one variable per call (frameId + path + new value), using the same path language as `read_variables`. Each call carries its OWN explicit consent flag (separate from `attach_process` consent), works only in break mode, and returns structured errors for type mismatch and read-only targets. Fail-closed: no consent means no write, and writes never cross debug session boundaries. `evaluate` stays read-only.

## User Stories

1. As an agent, I want to write one variable per call in the current debug session, so that I can inject a fault without restarting the target process.
2. As an agent, I want to address the write target with frameId plus path in the same path language as read_variables, so that I can read a value, then write it back with no translation step.
3. As an agent, I want each write call to carry its own explicit consent flag, so that no earlier attach consent can ever silently authorize a mutation.
4. As an agent, I want a write without consent to fail closed with no state change, so that an omitted flag is always safe.
5. As an agent, I want writes to require break mode, so that I never mutate a running target process mid-execution.
6. As an agent, I want a write attempted while running or in design mode to be rejected with a mode error, so that I know to pause first.
7. As an agent, I want a type-mismatched value to be rejected with a structured type-mismatch error, so that I learn the expected type without corrupting state.
8. As an agent, I want a write to a read-only target (const, read-only property, out-of-scope symbol) to be rejected with a structured read-only error, so that I stop retrying impossible writes.
9. As an agent, I want a write to a nonexistent path to be rejected with a not-found error naming the frameId and path, so that I can re-read the frame first.
10. As an agent, I want writes scoped to my own debug session, so that my fault injection never leaks into a sibling debug session bound to another VS instance.
11. As an agent, I want a successful write to echo the mode and the affected frameId plus path, so that the mutation is auditable in the transcript.
12. As an agent, I want `evaluate` to stay read-only after this change, so that casual expression probes can never mutate state by accident.
13. As an agent, I want the new value to round-trip through read_variables afterwards, so that I can verify the write took effect before resuming.
14. As a builder, I want write behavior verified without a live VS instance, so that fault-injection logic is testable in CI.

## Implementation Decisions

- New session-scoped write operation on the IDebugBridge seam plus a thin `set_variable` MCP tool beside `read_variables` / `evaluate`; tools stay thin and all debugger access crosses the seam.
- Request contract: session id, frame identifier, variable path in the `read_variables` path language, new value in the same value representation `read_variables` returns, and an explicit per-call consent boolean. No batch writes; one variable per call keeps each mutation auditable.
- Consent model: the write consent flag is independent of `attach_process` consent and defaults to absent/denied; the bridge checks it before touching debugger state (fail-closed), mirroring the existing attach consent gate but never sharing its flag.
- Mode gate: writes are accepted only when the session mode is break; any other mode yields a structured mode error and no mutation, consistent with the existing rule that inspection tools need break mode.
- Path resolution reuses the `read_variables` resolution semantics (frame-relative drill-down via expandable paths); unknown paths, stale frame ids, and ambiguous paths are resolution errors, never partial writes.
- Value coercion is strict: the bridge attempts conversion to the target type and rejects mismatches with a structured error carrying expected versus provided type information; no silent truncation or locale-dependent parsing.
- Read-only targets (constants, read-only properties, optimized-away locals, out-of-scope symbols) are detected before mutation and reported with a structured read-only error; the debugger state is untouched.
- Session isolation: the write resolves the frame within the calling debug session only; cross-session frame ids are invalid, so a write can never affect a sibling debug session or a different VS instance.
- Glossary vocabulary governs naming and errors: debug session versus VS instance versus target process, mode versus lastHit, and consent in the Mine/consent sense.
- `evaluate` semantics are unchanged: assignments, statements, and compound expressions stay rejected; the only supported mutation path is the new consent-gated tool.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and error shapes. No assertions on COM internals, call order, or private state.
- Tested surface: successful write reflected by a subsequent fake read; missing-consent rejection with unchanged state; wrong-mode rejection; type-mismatch structured error; read-only-target structured error; unknown-path error; cross-session frame rejection.
- Prior art: FakeDebugBridge as the in-memory test double, in the style of the existing read/evaluate tool-boundary tests — seed fake frames and variables, drive the tool boundary, assert envelopes and state, no VS needed.
- Live coverage stays manual: attach, break, read a variable, write it with consent, re-read to confirm, resume and observe behavior.

## Out of Scope

- Any change to `evaluate` read-only semantics; no assignment smuggling through expressions.
- Multi-variable transactions, conditional writepoints, or watch-triggered writes.
- Writing registers, memory addresses, or object vtables — only the `read_variables` path language is supported.
- Persisting consent across calls; every write re-states consent.
- Undo/redo of writes; re-writing the old value is the rollback.
- Cross-session or session-less writes.

## Further Notes

- Agents should read before writing: resolve the path with `read_variables` first so type-mismatch and not-found errors stay rare.
- Atomicity is per call: a failed write leaves the target unchanged, but consecutive successful writes are independent — there is no batch rollback.
