# Troubleshooting

Failure playbook, by symptom. Error codes are defined in the
[TOOLS.md error catalog](TOOLS.md#error-catalog).

- Setup and environment: [elevation mismatch](#elevation-mismatch-elevation-mismatch), [EnvDTE `HintPath` misses](#build-envdte-hintpath-misses), [exe locked on republish](#exe-locked-on-republish)
- VS state: [busy / modal dialog](#vs-busy--modal-dialog-vs-busy-timeout), [dead VS instance](#dead-vs-instance-vs-not-running-per-session)
- Targets and breakpoints: [PID recycle](#pid-recycle-process-not-found), [wrong process attached](#wrong-process-attached), [breakpoint stays unbound](#breakpoint-stays-unbound)
- Evaluation and results: [side-effect refusal](#side-effect-refusal-invalid-expression), [empty `lastHit`](#empty-lasthit), [null exception `message`](#exception-break-with-a-null-message), [exception setting did not stop](#exception-settings-did-not-stop-the-target), [write refused](#write-refused-read-only-target-type-mismatch)

## Setup and environment

### Elevation mismatch (`elevation-mismatch`)

Symptom: access-denied talking to VS. Cause: bridge and VS run at different
integrity levels. Fix: restart the bridge at the same elevation as VS — both
elevated or both not. Same Windows user is always required.

### Build: EnvDTE `HintPath` misses

Symptom: build fails resolving `EnvDTE*` references. Cause: the `HintPath`s in
`src/VsDebugMcp/VsDebugMcp.csproj` resolve from `$(VSINSTALLDIR)`, else the
VS 2026 Enterprise, Professional, then Community default path
(`C:\Program Files\Microsoft Visual Studio\18\<Edition>\…`); a non-default
install path matches none. Fix: build from a VS Developer prompt (sets
`VSINSTALLDIR`) or set it yourself. The references are build-time only
(embedded interop); the runtime binds DTE from the ROT.

### Exe locked on republish

Symptom: publish/overwrite fails while the client runs. Cause: the MCP host
locks the exe. Fix: stop the host (close/restart the client) before
republishing, then restart the client to load the new binary. Or publish to a
fresh folder and repoint the client config.

### Stale publish dir: WMI tools throw `PlatformNotSupportedException`

Symptom: `list_processes` and consent:true `attach_process` fail with a bare
`An error occurred invoking '<tool>'` (server stderr:
`System.Management.ManagementObjectSearcher..ctor` throws
`PlatformNotSupportedException: System.Management currently is only supported
for Windows desktop applications`), while every other tool works. Cause:
`dotnet publish` does not clean the output dir, so a stale 74 KB
`System.Management.dll` placeholder (NuGet `lib/net10.0` asset) can survive
and shadow the real 314 KB Windows implementation (`runtimes/win` asset):
check the published `System.Management.dll` size. Fix: publish to a clean
(empty) folder and repoint the client config, then restart the client. Verify:
the fresh folder's `System.Management.dll` is ~314 KB and a direct-stdio
`list_processes` call returns `IsError = False`.

## VS state

### VS busy / modal dialog (`vs-busy`, `timeout`)

Symptom: calls fail with `vs-busy` or time out while VS shows a dialog or is
otherwise occupied. Fix: dismiss any modal dialog in VS and retry. `resume`
and `step` return `status: timeout` instead of hanging — retry or `pause`.
All sessions share one dispatcher thread, so one wedged VS stalls every
session behind it. A call that times out (or is cancelled) while still
queued is dropped, never run late: a reported `timeout` means no stale `Go`,
step or launch fires once VS frees up. Work already running on VS is not
interrupted.

### Dead VS instance (`vs-not-running`, per-session)

Symptom: one session fails with `vs-not-running` while the others keep
working. Cause: its bound VS exited. Fix: `detach` the dead session (or
re-attach the same `instanceOverride` for a fresh binding); sibling sessions
are unaffected and keep their ids.

## Targets and breakpoints

### PID recycle (`process-not-found`)

Symptom: `attach_process` fails right after a successful `list_processes`.
Cause: PIDs are recycled by the OS — the process exited between list and
attach. Fix: list, then attach promptly; on `process-not-found`, re-list.

### Wrong process attached

`list_instances` PIDs are VS IDE processes (`devenv`) — never pass one to
`attach_process`. Match the target program by name in `list_processes` and
check the `mine` flag before attaching with `consent: true`.

### Breakpoint stays unbound

Symptom: `list_breakpoints` keeps showing `bound: false`. First check
`list_modules(sessionId, nameFilter: <module>)` in break or run mode: no entry
means the module is not loaded yet (run further, then poll again); `symbolState:
missing` means no PDB was loaded for it (build Debug, keep the `.pdb` beside the
binary). Live VS reports only `loaded`/`missing`; `unknown` means no
information, not "fine". In design mode `list_modules` is `not-running`.

`run_to_cursor` fails with `unbound-location` (temporary breakpoint already
removed) when the destination does not bind. Live VS only reports this when it
rejects the location; a pending one runs to the timeout, so on a still-running
result check the path and `list_modules` as above.

## Evaluation and results

### Side-effect refusal (`invalid-expression`)

Symptom: `evaluate` (or a drill-down `path`, or a logpoint hole showing
`(unavailable)`) fails although the expression is valid C#. Cause: the live
bridge evaluates in no-side-effects mode (`, nse`,
[ADR-0010](adr/0010-no-side-effects-eval.md)); a method call or property
getter VS judges side-effecting is refused instead of run. Fix: read fields
or plain properties instead (`list.Count`, `s.Length`); there is no override.

### Empty `lastHit`

Symptom: results carry `lastHit: null`. This is normal: `lastHit` is only set
for breakpoint hits (`file:line` via `BreakpointLastHit`). Stepping,
exception breaks, and design/run states report null. An exception break
carries `exception` instead (shape in the
[TOOLS.md envelope](TOOLS.md#execution-in-executiontools)); drill
further via `read_variables(path: "$exception")` or `evaluate`
(`$exception.Message` works; method calls like `$exception.ToString()` do not).

### Exception break with a null `message`

Symptom: the result has `exception.message: null` and the note says the message
is unreadable. Cause: the no-side-effects evaluator refused (or failed) the
`Message` getter, which often builds text from resources. The break itself is
real. Fix: `read_variables(f0, path: "$exception")` and read the backing
field (e.g. `_message`). Record which exception types do this here.

### Exception settings did not stop the target

Symptom: `set_exception_setting(..., "first-chance")` succeeded but the throw
did not break. Check `list_exception_settings(nameFilter: <type>)` shows
`first-chance`, and that the thrown type is exactly that name (a derived type
has its own entry). Changes need break or design mode: a change attempted while
running fails with `already-running`. Settings live in the VS instance and may
persist with the solution: reset with `default` when done.

### Write refused (`read-only-target`, `type-mismatch`)

Symptom: `set_variable` fails and the value is unchanged. `type-mismatch`
means the value does not convert exactly to the type `read_variables` shows
(e.g. `1.5` into an `int`, `1,5` with a comma, an unquoted string), or the
target is not a primitive, `string` or nullable; pass it as `read_variables`
prints it. `read-only-target` means the evaluator refused the assignment or
the re-read showed the old value: constants, get-only properties and
optimized-away locals cannot change. Rewriting a value in a different
non-numeric spelling than VS shows (an escaped string written another way)
can also read as unchanged; write the exact shown form. Fix: write the
backing field or the local instead, or rebuild Debug for optimized code.
