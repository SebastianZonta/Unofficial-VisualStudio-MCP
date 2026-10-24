# VSMCP

Local MCP server driving a running Visual Studio 2026 debugger via COM automation.

## Language

**VS instance**:
A single running Visual Studio process registered in the ROT.
_Avoid_: session, process (when meaning VS itself)

**Debug session**:
One of many live MCP-side handles; each binds exactly one VS instance to
subsequent tool calls, isolated from sibling sessions. Released with `detach`.
_Avoid_: connection, instance

**Instance override**:
Optional ROT display-name prefix selecting which VS instance to bind.
_Avoid_: instance id, instance name

**Solution**:
The `.sln` open in a VS instance, owner of one or more projects.
_Avoid_: project, repo, workspace

**Project**:
A single `.csproj` loaded in a solution, unit matched by name for routing.
_Avoid_: solution, program, service

**Target process**:
A local OS process the debugger binds to through attach or launch, such as the API under test.
A debug session owns at most one live target process.
_Avoid_: VS instance, debug session

**Launch / attached from birth**:
`launch_process` starts a target process with the debugger bound before any
user code runs (started suspended, attached, then resumed), so breakpoints set
before launch hit at startup. Late attach is `attach_process` only.
_Avoid_: run, start debugging (when meaning launch_process)

**ROT**:
The running-object table: the OS registry of live COM objects. VS instances
are discovered by enumerating it; the server never hardcodes a versioned
program id.
_Avoid_: registry, process list (when meaning VS discovery)

**Session-less**:
A tool needing no session id (`list_instances`, `list_processes`). Discovery
works before any attach and never mutates debugger state.
_Avoid_: unauthenticated, detached

**Mine / consent**:
`mine` flags a target process the same-user gate will accept. `consent` is the
explicit `attach_process: true` confirmation for arbitrary-process attach.
`set_variable` carries its own per-call `consent` to mutate target state; it is
never shared with, or implied by, attach consent. Unknown owners fail closed (not mine).
_Avoid_: owned, permission (when meaning these two gates)

**Mode / last-hit**:
`mode` is the debugger state (`design`/`run`/`break`, or `unknown` when a state
read timed out or VS rejected it) echoed on every result.
`lastHit` is the `file:line` of the breakpoint last hit (null otherwise) —
the only per-frame file/line source.
_Avoid_: status, state (when meaning mode); hit (when meaning lastHit)

**Condition / hit count / logpoint**:
A breakpoint may carry a read-only `condition` (break only when true), a
`hitCount` N with mode `exact` (Nth hit only) or `every` (every Nth hit;
`HitCount` reports hits so far), or a `logMessage` template with
`{expression}` holes. A logpoint never stops and never sets `lastHit`; its
formatted output buffers in the session `Log`, read back via
`list_breakpoints`. Conditions, hit counts, and their combination are native in
VS (break-when-true plus equal/multiple, applied together); logpoints are
emulated evaluate-then-decide with auto-continue, because native log output (VS
Output window) is not automation-readable.
_Avoid_: tracepoint (when meaning logpoint), watch (when meaning condition)

**Breakpoint id**:
A stable identity minted per debug session (`bp<N>`, only goes up, never
reused or renumbered) for a breakpoint that debug session created. A
**foreign breakpoint** is any other breakpoint in the VS instance (set by the
user or another debug session): it shows as `other<N>` (positional, recomputed
per listing) and is list-only, so `remove_breakpoint` never touches it, and
`set_breakpoint` on its location is refused (`breakpoint-exists`) rather than
claiming it. Setting a location this session already owns returns its id.
_Avoid_: index, position (when meaning a `bp<N>` id); user breakpoint, mine (when meaning foreign/owned)

**Run to cursor / temporary breakpoint**:
`run_to_cursor` runs a debug session's target process forward to a file/line
through a temporary breakpoint: placed by the call, removed before it returns
on every exit path (arrival, earlier hit, timeout, error). It never lists as a
persistent `bp<N>` and never touches owned or foreign breakpoints. An earlier
breakpoint hit on the way wins and becomes `lastHit`. A breakpoint already at
the destination is reused instead of a temporary one.
_Avoid_: cursor (when meaning the editor caret), one-shot (when meaning a hit-count breakpoint)

**Module / symbol state**:
A module is a binary loaded into a debug session's target process, listed by
`list_modules`. `symbolState` is debugger-reported: `loaded` (detail is the
symbol path), `missing`, `failed` (detail is the reason), or `unknown` (no
information, never "fine"). An unbound breakpoint whose module is absent or
not `loaded` explains itself.
_Avoid_: assembly, DLL (when meaning module); PDB status (when meaning symbolState)

**Exception setting / exception break**:
An exception setting is one CLR exception type's break mode in a debug
session's VS instance: `first-chance` (break when thrown) or `unhandled-only`.
An exception break is a stop caused by an exception; its result carries the
`exception` payload (`type`, `path` `$exception`, `message`) and no `lastHit`.
`[Exception]` frames in `read_stack` stay the after-the-fact view.
_Avoid_: exception filter, catch point (when meaning exception setting)

**Truncation / expandable**:
Values over 500 chars are cut with a `[truncated]` marker. `expandable`
marks drillable values; its `path` feeds back into `read_variables`.
_Avoid_: clipping, collapsed

**Runner**:
A single E2E subagent owning one slice of the parallel pass: its own VS
instances (which it starts and no other runner touches), its own debug
session, its own target processes, its own Sandbox file path, and its own
partial report. Three runners (R1, R2, R3) run concurrently against the same
revision.
_Avoid_: session (when meaning the runner itself)

**Slice**:
One runner's assigned phase range, always including its own P0 setup:
R1 = P0 + P1–P4 + P9, R2 = P0 + P5–P7, R3 = P0 + P8 + P10–P11. Boundaries are
fixed by the unbreakable chains (P5's shared stopped target, P8's live-target
chain and exception M0 chain, P10's self-contained two-VS flow — none cross a
runner boundary; P9 travels with its runner's own P0 wait PID).
_Avoid_: phase (when meaning a runner's whole assignment)

**Run tag**:
The shared `<yyyy-MM-dd-HHmm>` timestamp naming one parallel run. Each
runner's partial report appends its R-suffix (`<tag>-R1.md` …); the main
agent merges the three partials into the single verdict file `<tag>.md`.
_Avoid_: timestamp (when meaning the shared run identifier)

**VS PID vs target PID**:
The VS PID identifies the IDE process (`devenv`, from `list_instances`) and
is never an attach target. The target PID identifies the program under test
(from `list_processes`, or returned by `launch_process`) and is the only valid
`attach_process` argument.
_Avoid_: pid (unqualified — always say which)
