# launch-process

Status: ready-for-agent

## Problem Statement

The MCP is attach-only: every debug run needs an externally started target process. In demos this meant hand-rolling `dotnet run` in a shell, then hunting the new target PID through `list_processes` before calling `attach_process`. That round-trip is slow, racy (the target PID may recycle before attach), and misses process startup entirely, so entry-point breakpoints can never bind — the target process is already past `Main` by the time the debugger attaches.

## Solution

Add a session-scoped `launch_process` tool: given an executable path plus arguments, working directory, and extra environment variables, start the target process with the VS debugger attached from birth, bound to the calling debug session. Entry-point breakpoints set before launch therefore bind at startup. The same-user gate applies (a debug session only launches as its own user); launching is NOT session-less — it requires a bound debug session. `attach_process` is unchanged.

## User Stories

1. As an agent, I want to launch a target process from a bound debug session by executable path, so that I never need an external shell to start the program under test.
2. As an agent, I want to pass command-line arguments to the launched target process, so that I can reproduce the exact invocation under test.
3. As an agent, I want to set the working directory of the launched target process, so that relative file lookups behave as in production.
4. As an agent, I want to supply extra environment variables for the launched target process, so that connection strings and feature flags flow into the run.
5. As an agent, I want the launched target process to start with the debugger attached from birth, so that entry-point breakpoints bind before user code runs.
6. As an agent, I want breakpoints set before launch to hit on startup, so that I can debug initialization code I can never reach via late attach.
7. As an agent, I want launch to require a bound debug session, so that the new target process is always owned by exactly one debug session.
8. As an agent, I want the same-user gate to apply to launch, so that a debug session cannot start a target process as a different user.
9. As an agent, I want the launch result to carry the new target PID plus mode/lastHit, so that follow-up inspection tools work exactly as after `attach_process`.
10. As an agent, I want a clear failure when the executable path does not exist or is not runnable, so that I can fix the path instead of debugging a phantom target process.
11. As an agent, I want `attach_process` behavior unchanged, so that my existing attach flows keep working alongside launch.
12. As an agent, I want launch to fail rather than attach to a VS PID, so that a `devenv` path can never become its own debug target.
13. As a builder, I want launch behind the single bridge seam, so that tools stay thin and process-start plus debugger-bind stays in one place.
14. As a builder, I want the fake bridge to simulate launch (phantom target PID, mode transitions), so that launch flows are testable without starting real processes.

## Implementation Decisions

- New session-scoped launch operation on the IDebugBridge seam plus a matching `launch_process` MCP tool beside `attach_process`. No behavior change to `attach_process`.
- Tool contract: executable path (required), arguments string or list (optional, default empty), working directory (optional, defaults to a documented rule such as the VS instance working directory), environment map merged over the inherited environment (optional, default empty). Result contract: new target PID plus mode/lastHit, matching the `attach_process` result shape so downstream tools are uniform.
- Launch binds the new target process to the calling debug session exactly as `attach_process` does: one debug session owns at most one live target process; launching while a target process is already bound is an explicit error (detach or terminate first — no implicit kill).
- Debugger-attached-from-birth is the defining semantic: the bridge starts the target process suspended-or-equivalent under the VS debugger so breakpoints set before the call are already bound when user code starts. Late-attach behavior belongs to `attach_process` only.
- Same-user gate applies: the launch runs as the bridge user and the mine check treats the new target process as mine by construction. No `consent` parameter is needed because launch creates the target process rather than seizing an arbitrary one — but any cross-user launch attempt fails closed.
- NOT session-less: the tool requires a valid debug session id and resolves through the same session validation as the other session-scoped tools. Discovery tools remain the only session-less tools.
- Live OS handles never escape the bridge: the bridge snapshots the new target process to plain DTOs and maps start failures (missing executable, access denied, bad working directory) to structured errors with no partial session binding left behind.
- Glossary vocabulary governs naming and errors: VS instance versus debug session versus target process, VS PID versus target PID. Error text names which PID kind is expected wherever a PID appears.
- No new ADR is required unless launch changes the attach security model; if it does, the decision is recorded via a new ADR file in docs/adr/ that supersedes rather than edits prior decisions.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and session-binding rules. No assertions on OS start internals, debugger COM call order, or private state.
- Tested modules: the new launch tool at the tool-call boundary against the FakeDebugBridge; the bridge interface contract for session-required launch, argument/working-directory/environment passthrough, and attached-from-birth binding; error-shape tests for missing executable and already-bound session.
- Prior art: tool-boundary tests against the FakeDebugBridge in the style of the existing attach tests (fake returns a phantom target PID and mode transitions without touching the OS); session-validation tests in the style of the existing session-scoped tool tests; no VS needed, run with `dotnet test VsDebugMcp.slnx`.
- Live coverage stays manual: bind a debug session to a VS instance, set an entry-point breakpoint, launch a trivial executable with args plus working directory plus env, verify the breakpoint binds at startup and mode/lastHit report correctly.

## Out of Scope

- Any change to `attach_process` semantics, consent handling, or the same-user gate.
- Attaching by name, auto-discovery, or auto-reattach on exit (possible follow-up specs, not this one).
- Killing, terminating, or restarting target processes; launch-while-bound is an error, not an implicit kill.
- Remote machines, containers, elevated/other-user launches, or debugger transport selection.
- Persisting launch configurations across MCP restarts; each launch call carries its full parameters.
- Shell semantics: no shell Expansions, pipelines, or script execution — executable path plus explicit args only.

## Further Notes

- Agents should prefer `launch_process` whenever they need startup code covered, and `attach_process` plus `list_processes` only for already-running target processes.
- OS PID recycling still applies after launch: agents should use the returned target PID promptly and treat unexpected target-process exit as "re-launch or re-list" rather than reusing a stale target PID.
