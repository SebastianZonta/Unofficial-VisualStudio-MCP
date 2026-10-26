# Spec: split the DteDebugBridge god-module

## Problem Statement

As an agent developer driving a debug session, I see no change — but as a maintainer, every edit to live debugger automation lands in one ~2100-line module where debug-session lifecycle, breakpoint ownership, and execution control share locals. A breakpoint fix risks an attach race; a stepping fix risks breakpoint listing. There is nowhere to land the follow-ups (validation collapse, logpoint isolation, breakpoint identity) without growing the same file.

## Solution

From the outside nothing changes: the same debug sessions bind the same VS instances, own the same breakpoints, and report the same debugger mode and last-hit. Inside, the live bridge delegates to three focused internal modules behind the existing bridge seam — one owning the debug-session table, one keeping breakpoint ownership, one driving execution — so each future fix lands in exactly one place.

## User Stories

1. As an agent developer, I want attach to return my existing debug session when I re-attach the same VS instance, so that I never hold duplicate bindings.
2. As an agent developer, I want a dead VS instance binding to rebind or detach cleanly, so that a crashed IDE kills only its own debug session.
3. As an agent developer, I want parallel attaches against the same VS instance to converge on one debug session, so that racing setup calls stay safe.
4. As an agent developer, I want list_sessions to inventory live debug sessions session-lessly, so that discovery works before any attach.
5. As an agent developer, I want detach to release exactly one debug session, so that sibling sessions keep their breakpoints and modes.
6. As an agent developer, I want set_breakpoint to mint stable breakpoint ids that only go up, so that repeat listings never renumber my breakpoints.
7. As an agent developer, I want re-setting a location my debug session already owns to return its existing id, so that retries are idempotent.
8. As an agent developer, I want set_breakpoint on a location holding a foreign breakpoint to be refused with a clear error, so that I never accidentally claim the user's breakpoint.
9. As an agent developer, I want list_breakpoints to show foreign breakpoints as list-only positional entries, so that I can see but never remove them.
10. As an agent developer, I want remove_breakpoint on a foreign id to fail with guidance, so that user breakpoints are only removed in VS.
11. As an agent developer, I want pause to report break mode with the current last-hit, so that I know where the target process stopped.
12. As an agent developer, I want resume to run the target process with a time budget and report timeout distinctly, so that a still-running target is not mistaken for a stop.
13. As an agent developer, I want step over/into/out to move only my debug session's target process, so that sibling sessions are unaffected.
14. As an agent developer, I want mode-gated calls (e.g. stepping while running) to fail with a clear error, so that I get a remediation instead of silent COM behavior.
15. As a maintainer, I want the debug-session table to have exactly one owner, so that lifecycle races are fixed in one module.
16. As a maintainer, I want breakpoint identity rules in one module, so that the stable-vs-positional id contract is testable through one seam.
17. As a maintainer, I want execution mode snapshots in one module, so that stepping fixes never touch breakpoint listing.

## Implementation Decisions

- The external bridge seam stays frozen: all 22 operations keep their current signatures and observable behavior per ADR-0002. The split adds internal modules only; no tool, DTO, or wire change.
- Three new internal modules behind the seam: a session registry (debug-session table plus liveness probe, idempotent re-attach convergence, detach, per-session dead marking per ADR-0008), a breakpoint keeper (owned stable ids, foreign positional ids, occupied-location refusal, same-options idempotency, list/remove paths), and an execution driver (pause, resume with budget, step directions, mode snapshot and mode gates).
- Internal modules are concrete classes with no new interfaces: a single live adapter means the internal seams are hypothetical, so interfaces would be ceremony. The highest (and only) test seam remains the existing bridge seam.
- Exactly one module owns the session table and its lock; the other two borrow a single debug session's entry plus the shared STA dispatcher per call and never see the table.
- Live breakpoint handles stay confined to the breakpoint keeper and are touched on the dispatcher thread only, exactly as today. No snapshot-to-plain-data conversion in this pass; that belongs to the breakpoint-identity follow-up.
- Logpoint emulation, exception settings, and target-process attach/launch stay in the live bridge for a later pass. The in-memory fake bridge is untouched — no shared registry extraction across live and fake.
- No domain-language change and no ADR: this pass names code modules only, leaving the glossary and ADR-0002/ADR-0008 accurate as written.

## Testing Decisions

- A good test for this split proves unchanged external behavior through the bridge seam, not the existence of the new internal modules. Tests that reach past the seam into internals are rejected.
- What must hold: the full existing unit suite (which drives the whole tool surface through the in-memory fake) plus a clean build. No new live-VS run is required because observable behavior is unchanged by construction.
- New unit tests only if the session-registry rebind logic changes shape during extraction; otherwise the move is proven by the suite staying green.
- Prior art: the per-tool suites under the tests directory that assert attach/session/breakpoint/execution behavior through the fake bridge, and the contract tests pinning error codes and envelopes.

## Out of Scope

- Extracting logpoint settle emulation, exception-setting baselines, or target-process attach/launch into their own modules (later passes).
- Converting held live breakpoint handles to plain-data snapshots.
- Unifying stable, positional, and COM breakpoint identity beyond moving the current rules verbatim.
- Sharing session-table code between the live bridge and the in-memory fake.
- Collapsing the validate-then-map cluster, folding the mode/last-hit envelope, or any tool, DTO, envelope, or error-code change.
- Live-VS end-to-end runs beyond what the current suite already covers.

## Further Notes

- Agreed over grill-with-docs rounds Q1–Q9: seam frozen, three internals first, concrete classes, single table owner, RCW moved as-is, fake untouched, green suite as the bar.
- Natural next candidates after this lands: collapse the validate-then-map cluster (its re-check lives in the code this split moves), then isolate logpoint emulation, then unify breakpoint identity.
