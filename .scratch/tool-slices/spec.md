# Tool slices — per-tool-group work split

## Problem Statement

Every small change breaks an unrelated tool: a breakpoint option edit breaks `evaluate`, an envelope field added on two paths misses the third, a publish leaves a stale binary that breaks WMI tools. Work lands across the single `IDebugBridge` seam with no file ownership, and `.scratch/` tracks overlapping session scopes in parallel.

## Solution

Split all future work by tool group, not by layer. Each slice owns its tool file plus its bridge paths, fake paths, tests, and docs end to end. One slice in flight at a time unless slices are disjoint. Each slice passes build plus unit plus contract plus its live E2E slice before merge. Platform code (build/publish, COM dispatcher) is owned by dedicated slices no tool slice may edit.

## User Stories

1. As a maintainer, I want one slice to own `SessionTools` end to end, so that session, VS instance, target process, and launch changes do not leak into breakpoints.
2. As a maintainer, I want one slice to own breakpoints and modules, so that condition, hit count, logpoint, foreign breakpoint (`other<N>`), and symbol state changes stay in one place.
3. As a maintainer, I want one slice to own execution and exception settings, so that pause, resume, step, run to cursor (temporary breakpoint), and exception break changes share one envelope review.
4. As a maintainer, I want one slice to own inspection and writes, so that `read_stack`, `read_variables`, `evaluate` (read-only, top frame `f0`, no-side-effects mode), and `set_variable` (the only write, per-call consent) share one gate review.
5. As a maintainer, I want platform slices for build/publish and COM dispatch, so that STA threading, ROT binding, `vs-busy`, and stale publish failures do not ride along with feature diffs.
6. As a maintainer, I want a contract slice that pins params, defaults, `mode`/`lastHit`/`exception` envelope, and error codes, so that doc drift fails the build.
7. As an E2E runner, I want each slice mapped to its runner slice (R1/R2/R3) and run tag, so that live VS verifies what the fake cannot.
8. As a triager, I want `.scratch/README.md` to match disk and one directory per slice boundary, so that parallel specs do not collide on the session store.

## Implementation Decisions

- Split axis is tool group (Session / Breakpoints+Modules / Execution+Exceptions / Inspection+Write), not layer (bridge/fake/tools/docs). Layer splits caused the breakage.
- Seams (highest possible, existing preferred, ideal one):
  - Primary seam stays the single `IDebugBridge` seam per ADR-0002. Tool slices add methods only within their owned group; no new seam.
  - Shared rules stay in one home per codebase rule: breakpoint options gate, module filtering, exception config, variable-write coercion, run-to-cursor composition. Slices call them, never fork them.
  - COM boundary stays behind the single STA dispatcher with snapshot-to-DTO; live handles never escape. Per-session lanes (pending ADR) go in the session slice only.
  - No new seam for contracts: extend the existing docs-contract check to params/defaults/envelope rather than adding a second checker.
- Ownership (one writer per path):
  - Slice 0 Hygiene: `.scratch/README.md`, close or re-scope `exception-config-02` (already covered by exception E2E cases and ADR-0011), reconcile run-tag slice table vs glossary Slice entry.
  - Slice A Infra: build/publish files, host flag dispatch order, publish-to-empty-dir rule.
  - Slice B COM core: dispatcher, message filter, error-code map, ROT discovery and bind, VS PID vs target PID guard.
  - Slice C Session/Process/Launch: `attach`, `detach` (deletes own breakpoints only), `list_sessions`, `list_instances`, `list_processes` (session-less per ADR-0007), `attach_process` (consent plus same-user gate per ADR-0004, consent checked before bind), `launch_process` (attached from birth, no consent per ADR-0009, one target process per debug session). Includes per-session dispatcher lanes ADR, session labels, and follow-mode fingerprint in that order.
  - Slice D Breakpoints/Modules: `set_breakpoint` (own-repeat returns same id, foreign is `breakpoint-exists`), `list_breakpoints` (stable `bp<N>`, positional `other<N>` list-only), `remove_breakpoint` (own only), `list_modules` (loaded vs missing, never loads symbols). Respects ADR-0010 read-only rule for condition and log holes.
  - Slice E Execution/Exceptions: `pause`, `resume`, `step`, `run_to_cursor` (temporary breakpoint removed on every exit path, never lists), `list_exception_settings`, `set_exception_setting` (break or design only, persists in VS instance per ADR-0011). Exception breaks carry `exception` payload with null `lastHit`; every other stop carries none (ADR-0006 envelope).
  - Slice F Inspection/Write: `read_stack` (function plus module only), `read_variables` (expandable path drill, leaves evaluator in `f0`), `evaluate` (read-only, top frame `f0`, no-side-effects per ADR-0010, truncation at 500 chars), `set_variable` (sole write per ADR-0012, per-call consent never taken from attach, break mode only, strict literal coercion, re-read echo).
  - Slice G Contracts: tool-count plus params/defaults/envelope/error-assignment checks, audit E2E cases, tool reference and glossary wording.
- Ordering: 0, then A and B, then G contract extension, then C, then D/E/F. Session cluster lands dispatcher ADR first, then labels, then follow-mode last. D/E/F may run parallel only after C lands.
- Isolation: one worktree plus one `.scratch/<slice>/` per active slice, max one slice in flight unless disjoint per ownership above. Publish dirs stay gitignored and never shared.
- Vocabulary: VS instance vs debug session vs target process, mine vs consent, `mode` vs `lastHit`, breakpoint id vs foreign breakpoint, run to cursor vs temporary breakpoint, module vs symbol state, exception setting vs exception break, truncation vs expandable, runner vs slice vs run tag, VS PID vs target PID. Surface ADR conflicts explicitly.

## Testing Decisions

- Good tests assert external behavior at the seam, not implementation: tool call in, result envelope plus mode/lastHit/exception out. No COM internals in unit tests.
- Prior art: `FakeDebugBridge` suites run with no VS; `AuditE2ETests` run live only with `VSDEBUG_E2E=1` against `Sandbox/` markers; docs-contract test pins names and counts.
- Each tool slice adds or extends one fake unit case plus one live audit case proving the same assertion on both bridges (bridge parity: live is truth, fix the fake to match live).
- New parity pins required: every result type field set on every path that returns it; run-to-cursor composition (set, continue, conditional remove) driven against both bridges; detach deletes own breakpoints; attach consent never authorizes a write; already-bound ordering.
- Pre-audit gate is enforced: a bridge-behavior diff without a new audit E2E case and a live run is rejected.
- Per-slice gates: build with warnings as errors, unit suite, extended contract check, and the slice's assigned live E2E phases (R1/R2/R3 with shared run tag) before merge.

## Out of Scope

- New debugger features (threads view, named sessions, auto-reattach) beyond sequencing them into slices.
- VS version upgrades, new transports, or client-config changes.
- Rewriting the fake truthiness or log rendering beyond parity pins.
- Backfilling all historical E2E reports.

## Further Notes

- Seams proposed above match the single-seam architecture; confirm they match maintainer expectation before claiming slice 01. If a new seam is needed (e.g. per-session lanes), propose it at the dispatcher boundary only.
- `.scratch/README.md` staleness and untracked audit dirs are fixed in Slice 0 before any tool slice starts, to avoid double work.
- Status vocabulary: `ready-for-agent` means fully specified per triage labels; `resolved` marks done work. Do not mix old wayfinder `claimed` wording into new issues.
