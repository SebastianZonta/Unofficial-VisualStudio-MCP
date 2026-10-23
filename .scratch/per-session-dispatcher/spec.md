# Per-session dispatcher lanes
Status: ready-for-agent

## Problem Statement

All debug sessions share one STA dispatcher thread for COM calls into VS instances. One wedged, modal-blocked, or slow VS instance therefore head-of-line blocks every sibling debug session behind it: healthy sessions stall, timeouts pile up, and a single dead IDE degrades into a whole-bridge outage. The symptom is documented in TROUBLESHOOTING.md and README Known limits, but the architecture still couples every session's liveness to the slowest VS instance. Multi-session isolation (one debug session per VS instance, siblings unaffected by detach, per-session vs-not-running) holds for state but not for scheduling.

## Solution

Give each debug session its own dispatcher lane (preferred) or, if lanes prove unworkable, a single fair queue with pause-skip-ahead — such that a blocked or busy VS instance degrades to per-session `vs-busy`/`timeout` while sibling debug sessions keep working at normal latency. Ship behind a migration path that keeps the current single-lane dispatcher as a fallback. Record the design in a new ADR with explicit deadlock and starvation analysis. Safety gates (mine/consent, same-user) and all tool contracts are unchanged; only scheduling changes.

## User Stories

1. As an agent developer, I want a blocked VS instance to degrade to per-session `vs-busy`/`timeout` on its own debug session, so that one modal dialog does not stall my other sessions.
2. As an agent developer, I want a healthy sibling debug session to complete pause/resume/step at normal latency while another session's VS instance is wedged, so that multi-target debugging stays usable.
3. As an agent developer, I want `list_sessions` and session-less tools to stay responsive during any single-session blockage, so that discovery never wedges behind control calls.
4. As an agent developer, I want per-session timeouts to keep their existing semantics (`timeout` result, never a hang), so that lane changes do not alter the client-visible wait contract.
5. As an agent developer, I want detaching a wedged debug session to not require waiting for its blocked call, so that teardown always makes progress.
6. As an agent developer, I want attaching a new debug session to succeed while an existing session's VS instance is blocked, so that new work is not gated on old failures.
7. As an operator, I want a fallback to the current single-lane dispatcher, so that a lane regression can be contained without a new release.
8. As an operator, I want the lane design recorded in a new ADR (superseding via new files, never editing a released ADR), so that the threading rationale survives the implementer.
9. As a reviewer, I want explicit deadlock analysis (what locks, in what order, what a wedged COM call holds), so that lanes cannot reintroduce global blocking through a shared lock.
10. As a reviewer, I want explicit starvation/fairness analysis (why a hot session cannot starve siblings, lane teardown order), so that the fix is not a faster way to livelock.
11. As an agent developer, I want mine/consent and same-user gates enforced exactly as today, so that scheduler work never weakens the attach trust boundary.
12. As an agent developer, I want error codes unchanged (`vs-busy`, `vs-not-running`, `timeout`, `no-session`, per-session semantics), so that existing retry logic keeps working.
13. As an agent developer, I want a wedged session's recovery (dialog dismissed, VS responsive again) to resume normal latency without re-attach, so that transient blocks need no rebuild.
14. As a maintainer, I want FakeDebugBridge-level tests pinning the isolation contract, so that scheduling regressions are caught with `dotnet test VsDebugMcp.slnx` and no VS.

## Implementation Decisions

- Everything crosses the `IDebugBridge` seam; tools stay thin — scheduling lives behind the seam, never in tool classes, so the 15-tool surface and result envelopes are untouched.
- Design constraint: one dispatcher lane per live debug session (preferred), each lane serializing only its own session's COM calls; acceptable alternative is a single fair queue with pause-skip-ahead, but only if the ADR shows it meets the same isolation bar (blocked session never delays siblings past a bounded skip). The ADR must state which design shipped and why the other was rejected.
- Contracts that must not change: `IDebugBridge` method signatures and DTO shapes; mine/consent and same-user gates; per-session vs-not-running (only the dead session affected); `mode`/`lastHit` echo; truncation/expandable; session-less tools never mutate debugger state.
- Migration path: keep the single-lane dispatcher as a named fallback selectable at startup; default selects lanes; fallback and lanes share the `IDebugBridge` contract so tools cannot tell which is active; the ADR records the fallback's expected removal condition (or its retention as permanent escape hatch).
- `FakeDebugBridge` is the behavioral reference: it simulates a wedged session (blocked call) while sibling sessions complete, pins per-session `vs-busy`/`timeout` vs sibling success, and pins detach/attach progress during blockage — all without VS.
- Required ADR contents: lane lifecycle (creation on attach, teardown on detach, what a blocked call holds at teardown), lock ordering, COM apartment assumptions, deadlock analysis, starvation/fairness analysis, fallback selection, and why safety gates are untouched.
- Glossary discipline: `debug session` vs `VS instance` vs `target process` kept exact; lanes belong to debug sessions; `session-less` tools called out as never-queued-behind session lanes.

## Testing Decisions

- External behavior only, via `FakeDebugBridge` prior art with `dotnet test VsDebugMcp.slnx` (no VS needed): wedged-session isolation (sibling completes while one blocked), per-session `vs-busy`/`timeout` shape, session-less responsiveness during blockage, detach-during-blockage progress, attach-during-blockage progress, fallback-mode parity (same observable contract in both modes), gate behavior unchanged (consent-required/user-mismatch still fail closed).
- No timing-sensitive assertions beyond the existing timeout-result contract (over-budget waits return `timeout`, never a hang); no live-VS or COM-apartment tests; no throughput benchmarks as acceptance criteria.
- Deadlock/starvation analysis is a review artifact in the ADR, not a test; tests pin the observable isolation, the ADR argues the absence of global blocking.

## Out of Scope

- Changing tool contracts, error codes, or result envelopes; weakening mine/consent or same-user gates; cross-session transactions or atomic multi-session control; VS-side parallelism (one lane still serializes per VS instance); UI-thread fixes inside VS itself; automatic modal-dialog dismissal; persistence of lane state across restarts.

## Further Notes

- The alternative (fair queue with pause-skip-ahead) is explicitly allowed because per-VS COM apartment constraints may make true per-session STA threads costly — but the burden of proof is on the ADR to show skip-ahead meets the sibling-latency bar.
- Watch for shared-state traps that silently re-serialize lanes: caches, ROT enumeration locks, or logging locks held across COM calls. Name each in the ADR with its hold scope.
- Convention reminders: no test counts in README.md; TROUBLESHOOTING.md and README Known limits updated only to describe the new behavior; new decisions as new files in docs/adr/.
