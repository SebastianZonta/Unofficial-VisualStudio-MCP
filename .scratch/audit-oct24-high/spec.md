# Audit Oct24 high: no ghost COM, no self-attach, honest inspection and errors
Status: done

## Problem Statement

The 2026-10-24 ten-agent audit found seven high-severity defects. Each one either crashes or misleads the agent without a usable error, or breaks a documented promise:

1. Live COM handles escape the dispatcher thread and sessions resolve before the dispatch hop, so detach mid-call causes raw interop failures or work for a dead debug session.
2. The busy-VS retry filter is unrooted and can be collected while registered, risking a crash on the next busy callback.
3. `attach_process` accepts a VS PID (devenv) as a target process, so a debug session can attach the IDE to itself. Launch already refuses this.
4. `run_to_cursor` fails spuriously when the target process starts running between temp-breakpoint placement and continue, and can delete a pre-existing owned breakpoint on a List-then-Set race.
5. Live top-level variables never carry a drill path, so expandable values cannot be drilled per the truncation/expandable contract. The fake bridge does carry one, so tests pass and live fails.
6. The fake evaluator returns a canned value for unknown names while live throws invalid-expression, and the shared read-only gate lets generic-arity calls through.
7. Validation errors carry no machine-readable code channel and the COM-to-domain mapper drops the inner exception, breaking the error-code contract.

## Solution

- All session resolution and validation happens inside the single-dispatcher work item; only plain data crosses threads, never live handles. Detach during a call surfaces no-session or vs-not-running.
- The message filter instance is rooted for the process lifetime and the prior filter is preserved.
- Attach refuses a VS PID with invalid-argument naming the VS PID vs target PID distinction. Listing still shows VS instances.
- `run_to_cursor` joins an in-progress run instead of erroring on already-running, and only removes a temporary breakpoint it created.
- Top-level inspection values carry drillable paths; unknown-name evaluation fails the same on both bridges; the gate treats generic-arity calls as calls.
- Every validation failure carries its code in the documented data channel with unchanged wire text; domain errors preserve the causal exception.

Tool contracts stay the same: names, parameters, error codes, and the mode/lastHit envelope.

## User Stories

1. As an agent, I want a detach racing my read to give me no-session or vs-not-running, so that I get a retryable code instead of a raw interop failure.
2. As an agent, I want work queued for a debug session that no longer exists never to run against VS, so that timeout means nothing happened later.
3. As a developer, I want the server never to crash when VS is busy, so that a modal dialog cannot take down my session.
4. As an agent, I want attach_process with a VS PID refused with invalid-argument, so that I can never attach the IDE to itself.
5. As an agent, I want list_processes to keep listing VS processes for diagnosis while attach refuses them, so that discovery and safety do not conflict.
6. As an agent, I want run_to_cursor to succeed when the target process started running after my temp breakpoint was placed, so that a concurrent resume does not look like my failure.
7. As an agent, I want run_to_cursor to never delete a breakpoint it did not create, so that a concurrent set cannot destroy my setup.
8. As an agent, I want duplicate destination handling to reuse only a breakpoint at my exact destination, so that a same-named file elsewhere does not hijack my run.
9. As an agent, I want unbound destinations to fail with unbound-location on both fresh and reuse paths, so that missing symbols are reported not silently run through.
10. As an agent, I want expandable top-level variables to carry a path I can feed back into read_variables, so that drill-down works live as documented.
11. As an agent, I want a truncated scalar to remain drillable or explicitly terminal per contract, so that I never hit a dead expandable with no path.
12. As an agent, I want evaluate of an unknown name to fail with invalid-expression on both bridges, so that fake-based development matches live.
13. As an agent, I want generic-arity calls rejected by the read-only gate, so that one call rule governs all bridges.
14. As an agent, I want every validation failure to carry its code in the machine-readable channel, so that my retry logic does not parse message prefixes.
15. As an agent, I want error text unchanged while codes become readable, so that existing handling keeps working.
16. As a builder, I want domain errors to preserve the causal exception, so that diagnostics show the failing HRESULT not just the mapped code.
17. As a builder, I want the fake bridge to model live shapes for drill paths, evaluation errors and frame contents, so that tool-seam tests express the live contract.
18. As a maintainer, I want no new seams for these fixes, so that all behaviour stays behind the single debug-bridge seam.

## Implementation Decisions

- Concurrency: resolve the debug session and validate liveness inside the dispatcher work item under the existing gate; cross only snapshots and plain data between threads. Root the message-filter instance; preserve and restore any prior filter.
- Sessions: attach and list keep current consent and same-user order; add a target-name guard that rejects the IDE process name before binding. Identity remains the ROT display name; no schema change.
- Execution: the composed run-to-cursor keeps its place-then-run-then-remove shape; the continue step tolerates an already-running transition by joining the wait; removal is conditional on creation detected via a pre-existing id set.
- Inspection: top-level variable snapshots use the variable name as the drill path; evaluation errors for unknown names match the live invalid-expression shape; the invocation rule covers generic arity.
- Errors: single helper for validation failures that sets the documented code channel; domain exception gains an optional causal cause passed by the COM mapper on every arm. Wire text and code strings unchanged.
- ADRs respected: single debug-bridge seam; read-only evaluate with no-side-effects enforcement stays the backstop; consent gates and order unchanged; one live target process per debug session unchanged; CLR-only exception settings unchanged.

## Testing Decisions

- Good tests assert external behaviour at the highest seam: tool plus fake bridge, never live-handle internals or message text parsing.
- Modules tested: session attach/list, composed run-to-cursor, variable/evaluate inspection, read-only gate, error mapping.
- Prior art: tool-seam suites using the fake bridge (`AttachProcessTests.cs`, `RunToCursorTests.cs`, `InspectionTests.cs`, `ReadOnlyGateTests.cs`, `ToolValidationTests.cs`, `ComPlumbingTests.cs`); contract tests that pin tool counts and error-code rows (`DocsContractTests.cs`); concurrency tests via racing decorator bridges that flip mode between list and resume.
- Live-only confirmation (real VS instance, same user and elevation) for the dispatcher race, filter rootedness under busy VS, and self-attach refusal; plain CI stays VS-free.

### Required UTs (fake bridge, must pass in CI)

- `AttachProcessTests.Attach_refuses_devenv_pid`: seed `Processes=[devenv:1234]` + owner=current user; `attach_process(1234, consent:true)` → `invalid-argument` naming devenv. Guards fix 3.
- `RunToCursorTests.RunToCursor_joins_run_when_started_after_set`: racing decorator flips mode break→run between List and Resume; `run_to_cursor` → `ok` with destination awaited, temp removed. Guards fix 4a.
- `RunToCursorTests.RunToCursor_never_deletes_preexisting`: decorator seeds owned bp at destination between List and Set; after run the id survives in `list_breakpoints`. Guards fix 4b.
- `RunToCursorTests.RunToCursor_reuse_requires_bound`: seed unbound owned bp at destination; `run_to_cursor` → `unbound-location`, bp survives. Guards reuse path.
- `InspectionTests.TopLevel_expandable_has_path`: break at INSPECT fixture; `read_variables(f0)` expandable `order` asserts `Path=="order"`; `read_variables(f0, path:"order")` round-trips. Guards fix 5.
- `InspectionTests.Evaluate_unknown_fails_like_live`: `evaluate("order.Nope")` → `invalid-expression` on fake (was canned `41.80`). Guards fix 6a.
- `ReadOnlyGateTests.Rejects_generic_arity_call`: `foo<T>(x)` → `invalid-expression`; `a => a+1`, `(i==3)` still accepted. Guards fix 6b.
- `ToolValidationTests.Validation_errors_carry_Data_code`: every `ArgumentValidator` throw asserts `ex.Data["code"]` equals the prefixed code (covers `invalid-argument`, `invalid-expression`). Guards fix 7a.
- `ComPlumbingTests.ToDomain_preserves_inner`: `ToDomain(new COMException(...))` asserts `InnerException` is the input. Guards fix 7b.
- `ComDispatcherTests.Filter_instance_rooted`: `Register(); GC.Collect();` static root still alive via reflection. Guards fix 2.

### E2E guide (add to `docs/agents/e2e.md`, manual pass is authority)

- `E26` (P9, R1): `attach_process(<vsPidA>, consent:true)` → `invalid-argument` naming devenv; `list_processes` still lists it. Proves no self-attach live.
- `E27` (P8, R3): breakpoint at `HEARTBEAT`, `run_to_cursor HEARTBEAT` while a second concurrent `resume` is issued (or user presses F5 in VS between set and continue) → `break` at `HEARTBEAT`, no `already-running` spurious error, no leaked temp in `list_breakpoints`.
- `E28` (P8, R3): user sets a breakpoint at destination in VS during the `run_to_cursor` List→Set window (H-style manual step) → pre-existing id survives, `lastHit` is the destination.
- `I17` (P5, R2): `read_variables(f0)` top-level expandable carries non-empty `path`; `read_variables(f0, path:<that>)` lists members; `evaluate("order.Items[0].Sku")` equals drill value. Proves live drill path.
- `I18` (P5, R2): `evaluate("order.NoSuchField")` → `invalid-expression` live; `evaluate("foo<T>(x)")`-shaped generic call → `invalid-expression` (use a real generic member on Sandbox if present, else record OBS + gate UT as authority).
- `X1` (P1/P8, R1): validation probe `set_breakpoint(file:"", line:0)` → `invalid-argument` with machine-readable code echoed by host (check `Data["code"]` where host surfaces it); disconnect probe is manual (busy-VS dialog) → `vs-busy`, never crash. Automated suite covers all except the manual busy-VS step, mirroring the existing `BusyVs_StillSurfacesAfterRetryWindow` split.

## Out of Scope

- PID-recycling bind guards beyond the IDE-name check; per-engine attach selection; step lastHit shape unification; fake frame file/line and module failed-state doc cleanup; char-glyph and truncation edge cases. These move to the medium spec.
- E2E harness changes; new MCP tools or parameters; error-code renames.

## Further Notes

- Source findings A1, A2-part, A4-part, A5-part, A7-part, A8-part, A9-part from the 2026-10-24 swarm; duplicates merged.
- The generic-arity gate tweak keeps lambdas and comparisons accepted; verify live that pure generic calls are refused by no-side-effects mode.
