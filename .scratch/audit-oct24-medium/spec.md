# Audit Oct24 medium: honest sessions, budgets, modes and payloads
Status: ready-for-agent

## Problem Statement

The 2026-10-24 ten-agent audit found a second group of defects. None crashes like the high group, but each misleads agents, breaks a documented rule under parallel or edge use, or lets fake-based tests pass where live fails:

1. Dead debug sessions list as live; attach checks PID only so OS recycling can rebind the wrong target process.
2. Bridges accept non-positive lines; only the tool boundary validates.
3. Step success drops lastHit while the over-budget path keeps it; fake and live lastHit formats disagree.
4. Attach binds managed targets with the native engine while launch uses the managed engine, so breakpoints never bind after attach.
5. Fake stack frames carry file/line that live never provides; fake-only module states are documented as live states.
6. Variable-write rejects the space glyph it displays, fails open when the pre-read is unreadable, and masks disconnects as type errors.
7. The text gate passes unicode line breaks and comment-spliced calls; child drill paths always insert a dot, breaking indexers.
8. Instance listing double-prefixes error text; attach leaks raw timeout/COM; variable-write maps any automation failure to read-only-target; the exception live-check restore can skip detach.
9. Launch resume failures, empty working directories, env-block separators, stale-publish placeholder errors, exception-payload disconnects, untruncated fake exception text, unvalidated mode strings and fake default-restore all diverge from the fail-closed contract.

## Solution

- Session inventory lists only live debug sessions; attach optionally binds to an expected target name so recycled PIDs fail closed with process-not-found.
- Both bridges validate lines, modes and paths the same as the tool boundary; unknown modes are invalid-argument.
- Attach and launch use the same managed engine for managed targets; step and resume report lastHit in one shape; fake lastHit and frame shapes match live.
- Variable-write handles the displayed char glyph, fails closed on unreadable pre-reads and disconnects, and only maps genuine evaluator refusals to read-only-target.
- One single-line and invocation rule governs evaluate, conditions, logpoint holes and drill paths; indexer drill paths round-trip.
- Error surfaces keep single-prefix text and mapped codes on every path; the live-check harness always detaches and reports restore failures without hiding them.
- Launch, working-directory, env-block and owner-resolution helpers fail with domain codes, never bare runtime errors.

Tool contracts stay the same apart from rejecting inputs that previously slipped through with wrong success.

## User Stories

1. As an agent, I want list_sessions to omit dead debug sessions, so that I never act on a stale handle.
2. As an agent, I want attach to fail closed when a PID was recycled, so that I never debug the wrong target process.
3. As an agent, I want a non-positive line rejected by the bridge with invalid-argument and nothing created, so that tool and direct-bridge callers see one rule.
4. As an agent, I want step to report where it landed in the same shape on fast and over-budget paths, so that I can rely on lastHit.
5. As an agent, I want lastHit to always parse as file:line with no id prefix, so that one parser works on both bridges.
6. As an agent, I want attach to a managed target process to bind with the managed engine like launch does, so that my breakpoints bind.
7. As an agent, I want stack frames to carry no file/line on both bridges, so that fake-based navigation code works live.
8. As an agent, I want module symbol states limited to what live can emit, so that I never branch on a state that cannot occur.
9. As an agent, I want to copy a displayed char value back into set_variable and have it accepted, so that read-then-write round-trips.
10. As an agent, I want a refused write reported as refusal even when the pre-read failed, so that I never get false success.
11. As an agent, I want a VS disconnect during variable-write reported as vs-not-running or automation-failed, so that I retry instead of fixing my type.
12. As an agent, I want unicode line breaks and comment-spliced calls rejected by the read-only gate, so that one text rule holds.
13. As an agent, I want drill paths for indexer members to evaluate without edit, so that expanding a list works.
14. As an agent, I want instance errors single-prefixed and coded, so that my error handling parses once.
15. As an agent, I want attach timeouts mapped to timeout with remediation, so that every path reports the same budget error.
16. As an agent, I want only genuine evaluator refusals reported as read-only-target, so that transient failures are not mislabelled.
17. As an agent, I want launch resume failure reported as launch-failed, so that a still-suspended target process is never reported as ok.
18. As an agent, I want bad working directories and env-block separators rejected with invalid-argument, so that launch misconfiguration is actionable.
19. As an agent, I want owner-unknown processes to fail closed with user-mismatch, so that the mine/consent promise holds even when WMI is unavailable.
20. As an agent, I want exception payloads to survive VS disconnects as a break without payload, so that a dying target process does not surface raw errors.
21. As an agent, I want long exception messages truncated the same on both bridges, so that fake-based assertions match live.
22. As an agent, I want unknown exception modes rejected at the bridge, so that direct callers cannot corrupt later listings.
23. As a builder, I want the fake bridge to model live shapes for frames, modules, evaluation errors, truncation and defaults, so that contract tests pin parity.
24. As a maintainer, I want the exception live-check harness to always detach and count restore failures, so that runs leave no sessions or changed settings behind.

## Implementation Decisions

- Sessions: liveness is a filter on listing, not deletion of the record needed for rebind; recycled-PID protection threads an optional expected target name through the attach path and fails closed on mismatch. VS PID vs target PID vocabulary from the glossary is enforced at attach.
- Breakpoints and execution: line and mode validation lives in both bridges as well as the tool boundary; step hit reporting uses the post-step snapshot on both paths; destination reuse keeps exact-path semantics while listing keeps its documented suffix behaviour.
- Processes: managed attach uses the same engine constant as launch; launch resume checks its native return; start validation distinguishes missing directories from empty strings; env-block construction rejects separators and nul; owner resolution maps all hosting failure shapes to unknown-owner fail-closed.
- Inspection and variable-write: displayed char glyphs parse by first separator; silent-refusal detection distinguishes unknown-before from known-before; disconnect shapes propagate instead of being swallowed; drill-path joining omits the dot for bracket members; exception text truncation is shared.
- Errors and harness: single-prefix messages; dispatcher timeouts map to the timeout code; evaluator-refusal mapping is narrowed to genuine refusal shapes with the cause preserved; the harness nests restore in try/finally with detach guaranteed.
- ADRs respected: single debug-bridge seam; read-only evaluate and no-side-effects mode unchanged; per-call variable-write consent and attach consent order unchanged; one live target process per debug session; exception settings remain CLR-only with lastHit absent on exception breaks.

## Testing Decisions

- Good tests assert external behaviour at the tool-plus-fake-bridge seam, plus contract tests that read live-bridge shape (no failed states, no frame file/line, lastHit format) without needing VS.
- Modules tested: session inventory and attach guards, breakpoint validation, step/resume hit reporting, module listing, variable-write coercion and refusal, gate theories, error mapping, launch helpers, exception settings and payloads.
- Prior art: fake-bridge tool suites (`BreakpointTests.cs`, `OccupiedLocationTests.cs`, `ExecutionTests.cs`, `RunToCursorTests.cs`, `InspectionTests.cs`, `ReadOnlyGateTests.cs`, `SetVariableTests.cs`, `ListModulesTests.cs`, `ExceptionConfigTests.cs`, `ProcessLaunchTests.cs`, `ToolErrorsMappingTests.cs`); contract suites pinning tool counts, error-code rows and truncation limits (`DocsContractTests.cs`, `TruncationContractTests.cs`); gate theory tables for accepted comparisons and lambdas vs rejected assignments and calls.
- Live-only confirmation for engine selection on attach, indexer drill round-trip, char-glyph write and long exception truncation; plain CI stays VS-free.

### Required UTs (fake bridge, must pass in CI)

- `BreakpointTests.Bridge_rejects_nonpositive_line`: `SetBreakpointAsync(line:0)` direct on bridge → `invalid-argument`, nothing listed.
- `ExecutionTests.Step_reports_lastHit_both_paths`: stub break-on-step asserts fast-path and over-budget `ExecutionResult.LastHit` equal; fake `LastHit` matches `^.+:\d+$` with no `bp` prefix.
- `ListModulesTests.Live_shape_only`: contract test asserts live bridge source emits no `SymbolStates.Failed` / `Loaded:false`; fake `Loaded:false/Failed` rows replaced by live-reachable `Missing/Unknown`.
- `InspectionTests.Frames_carry_no_file_line`: canned `read_stack` frames assert `File is null && Line is null` like live.
- `SetVariableTests.Char_space_glyph_roundtrips`: `Coerce("char","32 ' '")=="' '"`; `IsSameShownValue("32 ' '","' '")` true.
- `SetVariableTests.Refused_when_before_unknown`: pre-read null + re-read != literal → refusal, never false success; disconnect-shaped `COMException` propagates as `vs-not-running`.
- `ReadOnlyGateTests.Unicode_and_comment_calls_rejected`: U+2028/U+2029/VT/FF/NEL, `foo /*c*/ (1)` → `invalid-argument`; `StripBlockComments` keeps `x => x.Id>0`, `(i==3)` accepted.
- `InspectionTests.Indexer_drill_path_roundtrips`: fake `items[0]` path has no `.[` segment and re-evaluates without `invalid-expression`.
- `ToolErrorsMappingTests.Single_prefix_and_mapped_timeout`: instance error contains exactly one code prefix; dispatcher `TimeoutException` from attach maps to `timeout` domain code.
- `SetVariableTests.Only_genuine_refusal_is_readonly`: transient `RPC_E_CALL_REJECTED` on write → `vs-busy`, not `read-only-target`; `E_FAIL` refusal → `read-only-target` with cause preserved.
- `ProcessLaunchTests.Launch_helpers_fail_closed`: `ResumeThread=-1` → `launch-failed`; `CheckStart("foo.exe")` → domain error not NRE; `""` cwd → `invalid-argument`; `=`/`\0` env keys → `invalid-argument`; `PlatformNotSupportedException` from WMI → owner null → `user-mismatch`.
- `ExceptionConfigTests.Bridge_validates_mode_and_truncates`: unknown mode direct on bridge → `invalid-argument`; fake `$exception` paths truncate at 500 chars like live; `Default` restores session-seeded value.

### E2E guide (add to `docs/agents/e2e.md`, manual pass is authority)

- `E29` (P8, R3): `attach` to Sandbox managed target → breakpoints bind and stop at entry (not ntdll loader); proves managed-engine attach. Native-only regression noted as known limit.
- `T6` (P7, R2): `step(over)` from `STEP_START` landing on a breakpoint asserts identical `lastHit file:line` on fast and over-budget paths (force the latter with a tiny `timeoutMs` once).
- `I19` (P5, R2): drill `order.Items[0].Sku` via returned `path` with no manual edit; `read_variables(f0)` shows no `File/Line` on frames; `evaluate("order.Nope")` → `invalid-expression`.
- `W11` (P5, R2): `set_variable(f0,"note","32 ' '")`-shaped char round-trip on a real char local (or Sandbox `note` string analogue); refused-write and disconnect probes stay UT-only unless reproducible live.
- `E30` (P8, R3): `launch_process` with `workingDirectory:""` / missing dir / `=` env key → `invalid-argument`, no process starts; stale-publish placeholder probe stays UT-only.
- `E31` (P8/P9, R3+R1): `list_modules` asserts only `loaded/missing/unknown`, never `failed`/`Loaded:false`; `list_sessions` omits dead sessions after `Stop-Process vsPid` (M8 pattern); recycled-PID guard stays UT-only (expectedName path) unless a short-lived target is scripted.
- `E25b` (P8, R3, extends E25): kill target mid-restore window → restore reports failure, `detach` still runs, no leaked session. Automated suite covers all except busy-VS and kill-race manual steps.

## Out of Scope

- High-group fixes (COM lifetime, filter rootedness, self-attach, run-to-cursor races, drill paths, fake fallback, error code channel) covered by the companion high spec.
- New tools, parameters or engine selectors; native-only attach support; E2E suite expansion beyond the parity cases above.

## Further Notes

- Source findings A1-part, A2-part, A3, A4-part, A5-part, A6, A7, A8-part, A9-part, A10 from the 2026-10-24 swarm; duplicates merged.
- Attach-engine alignment changes behaviour for native-only targets; keep the decision to add an engine selector later rather than in this spec.
