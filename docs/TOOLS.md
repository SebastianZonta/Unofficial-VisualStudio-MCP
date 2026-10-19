# Tools reference

13 tools over stdio. Session id comes from `attach`; `list_instances` and
`list_processes` are session-less. See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
when a call fails.

## Session (in `SessionTools`)

| Tool | Params (default) | Notes |
|---|---|---|
| `attach` | `instanceOverride?` (omit = newest VS 2026) | Returns `sessionId`. A new attach replaces the old binding. |
| `list_instances` | — | Session-less, read-only. Entry: display name (use as `instanceOverride`), pid, version, solution name/path, project names (sorted, capped at 50), reachable flag, error text. |
| `list_processes` | `nameFilter?` = null (case-insensitive substring), `ownedOnly` = true | Session-less, read-only. Entry: pid (use as `attach_process` arg), name, owner, mine flag, error text. Sorted by name, then pid. |
| `attach_process` | `sessionId`, `processId`, `consent` = false (must pass `true`), `timeoutMs` = 30000 | Same-user gate + explicit consent. Solution processes attach frictionlessly. |

## Breakpoints (in `BreakpointTools`)

| Tool | Params | Notes |
|---|---|---|
| `set_breakpoint` | `sessionId`, `file`, `line` (1-based) | Returns id + bind state immediately; unbound carries a warning, never an error. |
| `list_breakpoints` | `sessionId` | Poll until unbound flips to bound. |
| `remove_breakpoint` | `sessionId`, `breakpointId` | — |

## Execution (in `ExecutionTools`)

| Tool | Params (default) | Notes |
|---|---|---|
| `pause` | `sessionId` | Fails with `not-running` unless running. |
| `resume` | `sessionId`, `timeoutMs` = 5000 | Over-budget waits return `status: timeout`, never a hang. |
| `step` | `sessionId`, `direction` = `over`\|`into`\|`out`, `timeoutMs` = 5000 | Requires break mode. |

## Inspection (in `InspectionTools`)

| Tool | Params (default) | Notes |
|---|---|---|
| `read_stack` | `sessionId`, `maxFrames` = 20 | Newest frame first. Frames carry function + module only (no file/line — EnvDTE exposes none). |
| `read_variables` | `sessionId`, `frameId`, `path?` = null, `maxVariables` = 50 | Omit `path` for top level; pass an expandable `path` to drill into members. |
| `evaluate` | `sessionId`, `expression`, `timeoutMs` = 5000 | Read-only. Empty expressions and `;` (statements) are rejected client-side. |

## Result envelope

Every session-scoped result carries debugger `mode` (`design`/`run`/`break`)
plus `lastHit` (`file:line` of the breakpoint last hit, null otherwise), so no
separate status call is needed. The two session-less list tools return only
their collections.

Values are truncated at 500 chars (`… [truncated]`) with an `expandable` flag
plus a drill-down `path` — large collections never flood context.

## Error catalog

Failures are structured `code: detail; remediation: …`:

| Code | When | Remediation |
|---|---|---|
| `no-session` | unknown session id | call `attach` first |
| `stale-session` | session replaced by a newer attach | use the latest sessionId |
| `vs-not-running` | no ROT instance (or override matched none, or bound VS gone) | start VS 2026 / check override / re-attach |
| `vs-busy` | VS rejected the call (RPC_E_CALL_REJECTED: modal dialog or blocked UI thread) | dismiss any modal dialog in VS and retry |
| `elevation-mismatch` | access denied talking to VS (integrity-level mismatch) | run bridge at the same elevation as VS (both elevated or both not) |
| `automation-failed` | other COM failure | dismiss dialogs and retry; restart VS if it persists |
| `timeout` | automation call exceeded its budget | dismiss dialogs and retry (with larger `timeoutMs` for waits) |
| `consent-required` | `attach_process` without `consent: true` | re-call with `consent: true` |
| `user-mismatch` | target process owned by another user (or owner unknowable — fails closed) | only attach to your own processes |
| `process-not-found` | no local process with that pid (exited or recycled) | re-list and retry promptly |
| `not-in-break-mode` | inspect/step/evaluate outside break mode | resume to a breakpoint or pause first |
| `not-running` | `pause` while not running | resume first, then pause while running |
| `already-running` | `resume` while already running | pause first to inspect |
| `invalid-argument` | empty file/breakpointId/expression, bad line, bad step direction, `;` in expression | follow the per-field guidance in the message |
| `invalid-expression` | expression parsed but did not evaluate | pass a read-only expression valid in the current frame |
| `breakpoint-not-found` | unknown breakpoint id | call `list_breakpoints` for valid ids |
| `frame-not-found` | unknown frame id | call `read_stack` for valid frame ids |
| `path-not-found` | unknown expandable path | drill down only via paths returned by `read_variables` |

`list_instances` never fails wholesale: unreachable/busy instances come back as
`reachable: false` entries with error text. `list_processes` skips processes
that exit mid-scan; unknown owners are excluded by default and flagged when the
filter is off.
