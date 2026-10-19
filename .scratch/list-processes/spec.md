Status: ready-for-agent

## Problem Statement

When the agent wants to debug a running program, `attach_process` needs a process id, but no MCP tool returns process ids. The agent must leave the MCP and get the id from the shell, which breaks the rule that all debug data flows through the MCP. This is the same gap class `list_instances` closed for VS instances: the selector key has no discovery tool.

## Solution

A session-less, read-only `list_processes` tool beside `attach_process`. The agent lists local candidate target processes (process id, name, owner, mine flag), finds the program under test by name, then calls `attach_process` with that id and `consent:true`. The existing same-user gate and consent gate do not change.

## User Stories

1. As an agent, I want to list local processes without a debug session, so that discovery works before any attach.
2. As an agent, I want each entry to carry process id, process name, and owner, so that I can identify the program under test.
3. As an agent, I want a mine flag per entry, so that I can see at a glance which processes the same-user gate will accept.
4. As an agent, I want owned-only filtering by default, so that other users' and system processes never flood my context.
5. As an agent, I want an optional name substring filter, so that I can jump straight to the program under test by name.
6. As an agent, I want entries sorted by name, so that the list scans deterministically.
7. As an agent, I want listing to never attach or disturb any process, so that discovery is safe to call freely.
8. As an agent, I want one unreadable process to be skipped or flagged without failing the whole list, so that a single locked process never blinds me.
9. As an agent, I want the attach consent and same-user gates unchanged, so that discovery adds no new way to attach to a process I should not touch.
10. As an agent, I want the VS-instance PID from `list_instances` to stay distinct from target process ids, so that I never pass a `devenv` id to `attach_process`.
11. As a builder, I want process enumeration behind the single bridge seam, so that tools stay thin and OS access stays in one place.
12. As a builder, I want name filtering, owned-only filtering, and ordering as pure functions, so that selection logic is testable without OS calls.

## Implementation Decisions

- Add a session-less, read-only list operation on the bridge seam plus a matching MCP tool beside `attach_process`. No behavior change to `attach_process`.
- Agent-side matching is the routing rule: the agent lists, finds the target process by name (and mine flag), then calls `attach_process` with that id plus `consent:true`. No server-side auto-attach or name-routing parameter.
- Entry contract per process: numeric process id usable directly as the `attach_process` argument, process name, owner string (empty when unknowable), mine flag comparing owner to the bridge user case-insensitively.
- Owned-only filtering defaults to true: entries whose owner is unknown or belongs to another user are excluded unless the caller opts out. Fail closed, mirroring the attach gate.
- Optional name substring filter matches case-insensitively against the process name; empty means no name filtering.
- List ordering is by process name, then process id, both ascending and deterministic.
- Enumeration snapshots the local process table plus per-process owner lookup to plain DTOs; live OS handles never escape. Per-process failures (exited mid-scan, locked, owner lookup denied) are skipped or flagged with error text while the rest succeed.
- Owner lookup reuses the existing WMI owner source and its fail-closed null convention; unknown owner means excluded under the default filter, flagged when the filter is off.
- Glossary vocabulary governs naming: VS instance versus debug session versus target process versus instance override. A `devenv` entry is a VS instance, never an attach target.
- `list_instances` is untouched; no target-process data is merged into it.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge interface results, and pure filter/ordering behavior. No assertions on OS internals, call order, or private state.
- Tested modules: the new list tool at the tool-call boundary against the in-memory fake bridge; the bridge interface contract for session-less listing, owned-only default, and name filtering; pure filter plus ordering unit tests without OS calls.
- Prior art: tool-boundary tests against the fake bridge in the style of the existing attach and list-instances tests; pure selection tests in the style of the existing discovery tests; fake-driven unreachable/flagged coverage without touching the real process table.
- Live coverage stays manual: list processes, find the program under test by name, attach with consent, verify the mine flag predicted the same-user gate correctly.

## Out of Scope

- Any change to `attach_process` semantics, consent handling, or the same-user gate.
- Attaching by name directly inside `attach_process` or server-side name resolution.
- Killing, starting, or otherwise managing OS processes.
- Remote machines or processes owned by other users beyond listing them when the filter is off.
- Merging target-process data into `list_instances` or per-instance debugged-process reporting (possible follow-up, not this spec).
- Persisting process history across MCP restarts; PIDs are transient by nature.

## Further Notes

- Process ids are recycled by the OS: the agent should list, then attach promptly, and treat process-not-found as "re-list, the process may have exited."
- If name collisions across users become common, the mine flag plus owner string is the tie-breaker; no extra ranking is specified here.
