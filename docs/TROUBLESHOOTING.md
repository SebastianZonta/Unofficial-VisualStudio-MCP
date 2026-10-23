# Troubleshooting

## Elevation mismatch (`elevation-mismatch`)

Symptom: access-denied talking to VS. Cause: bridge and VS run at different
integrity levels. Fix: restart the bridge at the same elevation as VS — both
elevated or both not. Same Windows user is always required.

## VS busy / modal dialog (`vs-busy`, `timeout`)

Symptom: calls fail with `vs-busy` or time out while VS shows a dialog or is
otherwise occupied. Fix: dismiss any modal dialog in VS and retry. `resume`
and `step` return `status: timeout` instead of hanging — retry or `pause`.
All sessions share one dispatcher thread, so one wedged VS stalls every
session behind it. A call that times out (or is cancelled) while still
queued is dropped, never run late: a reported `timeout` means no stale `Go`,
step or launch fires once VS frees up. Work already running on VS is not
interrupted.

## Side-effect refusal (`invalid-expression`)

Symptom: `evaluate` (or a drill-down `path`, or a logpoint hole showing
`(unavailable)`) fails although the expression is valid C#. Cause: the live
bridge evaluates in no-side-effects mode (`, nse`,
[ADR-0010](adr/0010-no-side-effects-eval.md)); a method call or property
getter VS judges side-effecting is refused instead of run. Fix: read fields
or plain properties instead (`list.Count`, `s.Length`); there is no override.

## Dead VS instance (`vs-not-running`, per-session)

Symptom: one session fails with `vs-not-running` while the others keep
working. Cause: its bound VS exited. Fix: `detach` the dead session (or
re-attach the same `instanceOverride` for a fresh binding); sibling sessions
are unaffected and keep their ids.

## Build: EnvDTE `HintPath` misses

Symptom: build fails resolving `EnvDTE*` references. Cause: the five `HintPath`s
in `src/VsDebugMcp/VsDebugMcp.csproj` point at VS 2026 **Community** in the
default path (`C:\Program Files\Microsoft Visual Studio\18\Community\…`).
Fix: retarget them to your edition/install path. They are build-time only
(embedded interop); the runtime binds DTE from the ROT.

## Exe locked on republish

Symptom: publish/overwrite fails while the client runs. Cause: the MCP host
locks the exe. Fix: stop the host (close/restart the client) before
republishing, then restart the client to load the new binary. Or publish to a
fresh folder and repoint the client config.

## Empty `lastHit`

Symptom: results carry `lastHit: null`. This is normal: `lastHit` is only set
for breakpoint hits (`file:line` via `BreakpointLastHit`). Stepping,
exception breaks, and design/run states report null. On an exception break
(`[Exception]` frames, no `lastHit`), inspect the `$exception` local
(`$exception.Message` works; method calls like `$exception.ToString()` do not).

## PID recycle (`process-not-found`)

Symptom: `attach_process` fails right after a successful `list_processes`.
Cause: PIDs are recycled by the OS — the process exited between list and
attach. Fix: list, then attach promptly; on `process-not-found`, re-list.

## Wrong process attached

`list_instances` PIDs are VS IDE processes (`devenv`) — never pass one to
`attach_process`. Match the target program by name in `list_processes` and
check the `mine` flag before attaching with `consent: true`.
