# 04 — Slices D/E/F: breakpoints, execution, inspection/write

Status: resolved

## Task

- Slice D: `set_breakpoint` (own-repeat same id, foreign `breakpoint-exists`, `other<N>` list-only), `list_breakpoints` (stable `bp<N>`, buffered logpoint log), `remove_breakpoint` (own only), `list_modules` (loaded vs missing, never loads symbols). Shared breakpoint-options gate stays in one home.
- Slice E: `pause`, `resume`, `step`, `run_to_cursor` (temporary breakpoint removed on every exit path, never lists; foreign destination reused as is), exception settings and breaks in the VS instance (break or design only, persist after detach). Exception envelope: `exception` payload with null `lastHit` on exception stops, none otherwise.
- Slice F: `read_stack` (function plus module only), `read_variables` (expandable path, stays in `f0`), `evaluate` (read-only, top frame `f0`, no-side-effects), `set_variable` (only write, per-call consent, break mode only, strict coercion, re-read echo). Truncation at 500 chars with drill path.
- Each slice proves envelope parity on every path returning its result type, on both bridges.

## Done when

- Fake plus live audit cases green for the slice's E2E phases; composition (`run_to_cursor`, detach deletes own, attach consent never authorizes write) driven against both bridges.
- `DocsContractTests` extended to params/defaults/envelope/error assignment for touched tools (or deferred to issue 05 with explicit pointer).

## Notes

Respects ADR-0006 envelope, ADR-0010 no-side-effects eval, ADR-0011 exception settings, ADR-0012 consent-gated write. Vocabulary: mode vs lastHit, breakpoint id vs foreign breakpoint, run to cursor vs temporary breakpoint, exception setting vs exception break, truncation vs expandable.

## Comments

- No code changes: all Done-when gates were already proven on both bridges
  at triage (verified 2026-10-24: 198/198 slice-D/E/F fake tests green,
  full suite 315/315, build clean).
- Slice D — fake: `OccupiedLocationTests` (foreign refused with
  `breakpoint-exists` + `other1`, own repeat returns same id, no second
  breakpoint), `BreakpointTests` (stable ids, removed ids never reused,
  foreign listed but not removable), `ConditionalBreakpointTests` (read-only
  gate on condition/log holes, logpoint buffers 20 readable hits without
  stopping, per-session log isolation + detach release),
  `ListModulesTests` (loaded vs missing ordering, design-mode honesty,
  read-only gate). Live: `StableIds_BreakpointIdentity` plus manual P3/P4
  (conditions, logpoints, symbol-honesty never-loads-symbols) per the 05 map.
- Slice E — fake: `RunToCursorTests` (arrival/timeout/unbindable/design-mode
  all assert zero breakpoints after; foreign destination reused and survives;
  busy-cleanup retried, persistently-busy stays owned and removable; sibling
  session undisturbed; reuse requires bound), `ExecutionTests`
  (pause/resume/step transitions, timeouts as data),
  `ExceptionConfigTests` (first-chance throw breaks with `LastHit: null` +
  payload; breakpoint hit carries no payload; change-while-running rejected;
  per-sibling isolation; 500-char truncation). Live: manual P8/E13/E21–E25 —
  no automated audit case yet, accepted in the 05 slice map.
- Slice F — fake: `InspectionTests` (capped frames, truncation + expandable,
  per-frame reads, evaluate stays in `f0`), `ReadOnlyGateTests`,
  `SetVariableTests` (consent never taken from attach, consent-before-mode,
  break-only, strict coercion, re-read echo, no sibling leaks),
  `TruncationContractTests`. Live: `ReadOnlyGate_NoSideEffects`,
  `FrameTrue_StackLevels`, `EvaluateBudget_FullTimeoutMsHonored`.
- Contract bullet defers to 05 with pointer: `DocsContractTests` already pins
  params/defaults/envelope for these tools (commit `0f38c2c`); nothing to add.
- Live cases last exercised in the 2026-10-24-1800 pilot (R1/R2/R3 PASS);
  not re-run headless here.
