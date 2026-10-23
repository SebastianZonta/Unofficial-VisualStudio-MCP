# Conditional Breakpoints, Hit Counts, and Logpoints

Status: done

## Problem Statement

Today `set_breakpoint` is file/line only: every hit stops the target process. Agents debugging loops, retries, or hot paths cannot say "stop only when this is true", "stop on the Nth hit", or "log a value and keep going". The workaround is break-resume cycling with `read_variables`/`evaluate` on every hit, which is slow, perturbs timing, and floods context. Meanwhile agents have no way to observe a value across many hits without stopping at all.

## Solution

Extend breakpoints with three orthogonal options on `set_breakpoint`: a condition expression (break only when true), hit-count modes (break on the Nth hit), and logpoints (evaluate a message template and log it without stopping). Conditions and log message expressions follow the same read-only expression rules as `evaluate` (no assignments, no statements). Breakpoints remain per-debug-session storage: a breakpoint set via one debug session is never visible under a sibling debug session. `list_breakpoints` reports the options and current hit counts; `remove_breakpoint` is unchanged.

## User Stories

1. As an agent, I want to set a breakpoint with a condition expression, so that the target process stops only when the condition is true.
2. As an agent, I want condition expressions restricted to the same read-only rules as `evaluate`, so that a condition can never mutate program state.
3. As an agent, I want a rejected write-like condition to fail with a clear error and no breakpoint created, so that I never get a silently unconditional breakpoint.
4. As an agent, I want to set a hit-count breakpoint that breaks on the Nth hit, so that I can skip loop warm-up iterations without manual resume cycling.
5. As an agent, I want hit-count semantics (exact Nth hit vs every-Nth) stated plainly on the breakpoint, so that I know whether the 2Nth hit will stop again.
6. As an agent, I want `list_breakpoints` to show each breakpoint's condition, hit-count setting, current hit count, and bound/unbound state, so that I can audit what will stop.
7. As an agent, I want to combine a condition with a hit count on one breakpoint, so that I can stop on the Nth time a predicate holds.
8. As an agent, I want to set a logpoint with a message template, so that values are logged across hits without stopping the target process.
9. As an agent, I want logpoint message expressions restricted to read-only evaluation, so that logging never mutates program state.
10. As an agent, I want logpoint output routed somewhere I can read back (event/log surface), so that logging without stopping is actually observable.
11. As an agent, I want a logpoint hit to never change debugger mode to break, so that the target process keeps running while I collect values.
12. As an agent, I want breakpoints (including conditional ones and logpoints) stored per debug session, so that a breakpoint set via debug session A is never visible under debug session B.
13. As an agent, I want detaching a debug session to release its breakpoints, so that stale conditional breakpoints never surprise a later session on the same VS instance.
14. As an agent, I want an invalid condition or message (empty, unparsable, write-like) rejected at set time, so that I learn immediately instead of at the Nth silent hit.
15. As an agent, I want the `mode`/`lastHit` contract unchanged (a conditional hit that does not stop leaves mode and `lastHit` untouched), so that my existing break-detection logic keeps working.
16. As a builder, I want condition/hit-count/logpoint behavior behind the single bridge seam, so that tools stay thin and EnvDTE specifics stay in one place.

## Implementation Decisions

- Extend the `set_breakpoint` operation on the IDebugBridge seam with optional condition, hit-count, and logpoint-message inputs; all default to absent, preserving today's unconditional file/line breakpoint exactly.
- Condition contract: a single read-only boolean-ish expression evaluated in the breakpoint's frame context; write-like content (assignments, statements) is rejected with the same rules as `evaluate`. Empty means unconditional.
- Hit-count contract: an exact-hit mode (break on the Nth hit only) plus a modulus mode (break every Nth hit); counts start at zero when the breakpoint is created and are reported on every `list_breakpoints` entry. N must be a positive integer.
- Logpoint contract: a message template with embedded read-only expressions evaluated per hit and appended to a session-scoped log; a logpoint never stops the target process and never sets `lastHit`. A breakpoint is either stopping (condition and/or hit count) or a logpoint, not both — setting a log message precludes stopping behavior.
- Combination rule: condition plus hit count compose (the hit counter increments per evaluation pass and the break fires when both the count mode and a true/empty condition agree); logpoints compose with neither.
- Storage is per debug session on the MCP side, mirroring existing breakpoint isolation: listings, hit counts, and log output under one debug session never leak into a sibling debug session bound to the same VS instance. `detach` releases that session's breakpoints and buffered log output.
- Validation happens before any debugger mutation: invalid file/line, bad N, unparsable or write-like condition/message all fail with an error naming the offending input and create nothing.
- Glossary vocabulary governs naming and results: debug session versus VS instance versus target process, mode/lastHit, bound/unbound; conditions never disturb mode except when they actually stop.
- No new attach, execution, or inspection semantics: `pause`/`resume`/`step`, `read_stack`/`read_variables`/`evaluate` contracts are untouched; conditional evaluation reuses the `evaluate` read-only rule set rather than inventing a second expression language.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and validation accept/reject behavior. No assertions on EnvDTE internals, COM call order, or private state.
- Tested surface: setting unconditional (back-compat), conditional, hit-count, combined, and logpoint breakpoints via the tool-call boundary against the FakeDebugBridge; `list_breakpoints` reflecting options plus hit counts; per-debug-session isolation (breakpoint set via session A invisible under B); log output retrievable per session without a mode change; rejection of write-like conditions/messages with nothing created; invalid N rejected.
- Prior art: tool-boundary tests against the FakeDebugBridge in the style of the existing breakpoint tests; fake-driven bound/unbound and isolation coverage without touching a real VS instance; pure validation tests for the read-only expression gate without COM calls.
- Live coverage stays manual: set each breakpoint kind against a real VS instance, verify stop/no-stop behavior, hit counts, and log output, then detach and confirm cleanup.

## Out of Scope

- New expression language features beyond what `evaluate` accepts; conditions and log messages reuse `evaluate` rules exactly.
- Editing an existing breakpoint's condition in place (remove plus re-set covers it).
- Tracepoints that run debugger commands, call functions with side effects, or break other sessions.
- Persisting breakpoints or log output across MCP restarts or VS instance restarts.
- Data breakpoints (break on memory write), exception settings, or function-name breakpoints beyond file/line.
- Symbol/module diagnostics (covered by the module-symbol-status spec, not this one).

## Further Notes

- EnvDTE deferred binding still applies: a conditional breakpoint on code whose symbols are not loaded stays unbound until symbols load, same as today — pair with module/symbol status when diagnosing why a condition never fires.
- If the underlying debugger cannot represent a combination natively (e.g. condition plus every-Nth), the bridge emulates it with evaluate-then-decide per hit and documents which combinations are emulated versus native.
