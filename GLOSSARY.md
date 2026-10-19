# VSMCP

Local MCP server driving a running Visual Studio 2026 debugger via COM automation.

## Language

**VS instance**:
A single running Visual Studio process registered in the ROT.
_Avoid_: session, process (when meaning VS itself)

**Debug session**:
An MCP-side handle binding one VS instance to subsequent tool calls.
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
A local OS process the debugger binds to through attach, such as the API under test.
_Avoid_: VS instance, debug session

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
Unknown owners fail closed (not mine).
_Avoid_: owned, permission (when meaning these two gates)

**Mode / last-hit**:
`mode` is the debugger state (`design`/`run`/`break`) echoed on every result.
`lastHit` is the `file:line` of the breakpoint last hit (null otherwise) —
the only per-frame file/line source.
_Avoid_: status, state (when meaning mode); hit (when meaning lastHit)

**Truncation / expandable**:
Values over 500 chars are cut with a `[truncated]` marker. `expandable`
marks drillable values; its `path` feeds back into `read_variables`.
_Avoid_: clipping, collapsed

**VS PID vs target PID**:
The VS PID identifies the IDE process (`devenv`, from `list_instances`) and
is never an attach target. The target PID identifies the program under test
(from `list_processes`) and is the only valid `attach_process` argument.
_Avoid_: pid (unqualified — always say which)
