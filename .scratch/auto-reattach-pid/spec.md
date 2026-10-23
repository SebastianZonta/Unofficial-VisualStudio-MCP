# auto-reattach-pid

Status: ready-for-agent

## Problem Statement

Target PIDs are transient: the OS recycles them, and restart-heavy workflows (edit-and-rerun, crash loops, `dotnet watch`) replace the bound target process out from under the debug session. In one demo session the target PID changed four times, and each time the agent only discovered the death on the next inspection call, then had to hand-drive the full recover loop: notice the stale target PID, re-run `list_processes`, fingerprint the right new target PID by hand, re-call `attach_process`. That loop is mechanical, slow, and error-prone — the wrong same-named target PID is one misread away.

## Solution

Add an opt-in per-session follow mode: when enabled, and only when the bound target process exits, the debug session watches via `list_processes` polling criteria (executable path plus arguments fingerprint captured at bind time) and automatically re-attaches to the new target PID running the same binary, reporting the run-ended event followed by the re-attached event. Default OFF with explicit opt-in per debug session; the session never follows across different binaries.

## User Stories

1. As an agent, I want follow mode off by default per debug session, so that no debug session ever re-attaches unless I explicitly asked for it.
2. As an agent, I want to opt a debug session into follow mode explicitly, so that restart-heavy programs under test stay debugged across reruns.
3. As an agent, I want to opt a debug session back out of follow mode, so that a later run stays detached after exit when I am done iterating.
4. As an agent, I want the follow fingerprint (executable path plus arguments) captured at bind time, so that the debug session knows exactly which binary it is allowed to follow.
5. As an agent, I want the debug session to report run-ended when the bound target process exits, so that I never mistake a dead target PID for a live one.
6. As an agent, I want the debug session to auto re-attach to the new target PID running the same binary, so that I skip the manual re-list plus re-attach loop.
7. As an agent, I want the re-attach event to carry the new target PID plus mode/lastHit, so that follow-up inspection tools work exactly as after a manual `attach_process`.
8. As an agent, I want the debug session to never follow a different binary, so that an unrelated target process with a colliding name can never hijack my debug session.
9. As an agent, I want no re-attach while the original target process is still alive, so that follow mode never steals or duplicates a healthy binding.
10. As an agent, I want breakpoints to survive the re-attach, so that my entry-point and loop breakpoints keep hitting across reruns without re-setting them.
11. As an agent, I want the same-user and consent gates to apply to every re-attach, so that follow mode adds no new way to attach to a target process I should not touch.
12. As an agent, I want follow state scoped to one debug session, so that sibling debug sessions bound to the same VS instance are unaffected.
13. As a builder, I want follow matching behind the single bridge seam, so that tools stay thin and the exe-path plus args fingerprint rule lives in one place.
14. As a builder, I want the fake bridge to simulate exit plus same-binary reappearance, so that follow flows are testable without real process churn.

## Implementation Decisions

- Follow mode is per-debug-session boolean state on the IDebugBridge seam (enable/disable operations) plus automatic behavior inside the existing session lifecycle. No new session-less tool; no change to `list_instances` or the session-less `list_processes` contract.
- Fingerprint contract: executable path plus arguments string captured when the debug session binds (via `attach_process` or the planned launch tool). A candidate from `list_processes` polling matches if and only if both fields agree under documented normalization (case and separator rules stated once); bare process-name equality is never sufficient, and a different binary never matches even when its file name collides.
- Trigger rule: only the exit of the bound target process arms the watch. While the bound target PID is alive, polling is inert — no speculative attach, no double-binding, no stealing a healthy target process.
- Event reporting: the debug session surfaces two ordered events on the next interaction — run-ended (old target PID, exit signal) then re-attached (new target PID plus mode/lastHit in the `attach_process` result shape). Breakpoints set before exit persist across the re-attach; mode/lastHit reset semantics match a fresh attach.
- Gates apply per re-attach: the same-user mine check and the explicit consent posture of the original bind carry over, and any candidate failing them is skipped (the watch continues rather than binding wrongly). Follow mode never widens attach authority.
- Opt-in posture: default OFF for every new debug session; enabling is an explicit per-session call, and `detach` clears follow state with the debug session. No global toggle, no persistence across MCP restarts.
- Live OS handles never escape the bridge: polling reuses the existing `list_processes` snapshot path and the bridge maps exit-detection races (target PID recycled between poll and bind) to "watch continues" rather than binding the wrong target process.
- Glossary vocabulary governs naming and errors: VS instance versus debug session versus target process, VS PID versus target PID, mode/lastHit. Error and event text always qualifies which PID kind it means.
- No new ADR is required unless follow mode changes the attach security model; if it does, the decision is recorded via a new ADR file in docs/adr/ that supersedes rather than edits prior decisions.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and follow matching outcomes. No assertions on polling internals, timers, COM call order, or private state.
- Tested modules: follow opt-in/opt-out at the tool-call boundary against the FakeDebugBridge; the bridge interface contract for default-off, fingerprint capture at bind, exit-then-rematch, different-binary rejection, and still-alive no-op; pure fingerprint-match unit tests without OS calls.
- Prior art: tool-boundary tests against the FakeDebugBridge in the style of the existing attach and list-processes tests (fake simulates target-process exit and same-binary reappearance with phantom target PIDs); pure matching tests in the style of the existing discovery filter tests; no VS needed, run with `dotnet test VsDebugMcp.slnx`.
- Live coverage stays manual: bind a debug session, opt into follow mode, kill and restart the same binary with identical args (verify run-ended then re-attached with the new target PID), then restart a different binary under a colliding name (verify no follow).

## Out of Scope

- Any change to `attach_process` semantics, consent handling, or the same-user gate beyond carrying them into re-attach.
- Launching processes (covered by the launch-process spec); follow mode only re-attaches to processes the OS already started.
- Fuzzy matching: no name-only, publisher-only, window-title, or heuristic ranking — exe path plus args fingerprint or nothing.
- Cross-binary, cross-user, cross-VS-instance, or cross-debug-session following.
- Polling tuning knobs (interval, timeout, backoff) beyond documented defaults; no user-facing scheduler configuration.
- Persisting follow state or fingerprints across MCP restarts; target PIDs are transient by nature.

## Further Notes

- OS PID recycling is the motivating hazard: agents should treat every target PID as stale-after-exit and let follow mode (or a fresh `list_processes` plus `attach_process` when follow is off) supply the new one — never reuse a dead target PID.
- Follow mode composes with debugger launch: a launched target process that exits and relaunches under the same path plus args is the canonical follow case.
