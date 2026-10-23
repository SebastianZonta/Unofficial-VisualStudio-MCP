# ADR-0009: Launch needs no consent flag

Status: accepted

## Context

ADR-0004 gates `attach_process` behind explicit `consent: true` plus a
same-user owner check, so seizing an already-running target process is always
a conscious act. `launch_process` binds a debug session to a target process
too, but it creates that process instead of seizing an existing one. Applying
ADR-0004 literally would demand consent; leaving it out needs a recorded
reason.

## Decision

`launch_process` takes no `consent` parameter. ADR-0004 stays in force for
`attach_process` and is narrowed in scope: consent guards binding to a target
process the bridge did not start. Launch keeps the rest of the gate by
construction:

- Same-user: the bridge starts the target process as its own user, with no
  credential or elevation parameter, so it is mine by construction and a
  cross-user launch cannot be expressed.
- No seizure: launch never binds an existing process, so there is no foreign
  process whose owner could be misjudged.
- Bounded input: a debug session is required, `devenv` is refused, the path
  must be absolute and a .NET 5+ build output, and arguments go verbatim to
  the OS with no shell expansion.

Alternative considered: a `consent: true` flag on launch for symmetry. Rejected
because the flag would not protect anything the agent did not already choose
by naming the executable. An agent can always pass `true`, so the flag would
be ceremony.

## Consequences

Starting a program under the debugger is one call with no extra friction. The
accepted risk: launch *executes* an agent-chosen program as the bridge user,
which is a stronger capability than attach. That is tolerable because an MCP
client driving this server typically already holds shell-level power on the
same machine. If the server is ever exposed to a less-trusted client, reopen
this decision with a new ADR (an allowlist of executable roots, or consent).
