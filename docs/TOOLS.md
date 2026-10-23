# Tools reference

16 tools over stdio. Session id comes from `attach` — one live session per VS
instance. `list_instances`, `list_processes`, and `list_sessions` are
session-less. See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) when a call fails.

## Session (in `SessionTools`)

| Tool | Params (default) | Notes |
|---|---|---|
| `attach` | `instanceOverride?` (omit = newest VS 2026) | Returns `sessionId`. One session per VS instance; re-attaching the same instance returns its live session. |
| `list_sessions` | — | Session-less, read-only. Entry: `sessionId`, display name. Join with `list_instances` for solution details. |
| `detach` | `sessionId` | Releases that session's VS binding (`DetachResult` echoes the id) and deletes the breakpoints the session created, so stale conditional breakpoints never surprise a later session. Siblings unaffected; unknown id is `no-session`. |
| `list_instances` | — | Session-less, read-only. Entry: display name (use as `instanceOverride`), pid, version, solution name/path, project names (sorted, capped at 50), reachable flag, error text. |
| `list_processes` | `nameFilter?` = null (case-insensitive substring), `ownedOnly` = true | Session-less, read-only. Entry: pid (use as `attach_process` arg), name, owner, mine flag, error text. Sorted by name, then pid. |
| `attach_process` | `sessionId`, `processId`, `consent` = false (must pass `true`), `timeoutMs` = 30000 | Same-user gate + explicit consent. Solution processes attach frictionlessly. |
| `launch_process` | `sessionId`, `executablePath` (absolute, not `devenv`), `arguments?` (verbatim, no shell expansion), `workingDirectory?` (default: the executable's directory), `environment?` (merged over the inherited environment), `timeoutMs` = 10000 | Starts a .NET 5+ target process with the debugger attached from birth: breakpoints set before launch bind and hit at startup. Waits up to `timeoutMs` for a first break like `resume`, returning the new target PID plus `mode`/`lastHit`. No `consent` needed: the bridge creates the process as its own user ([ADR-0009](adr/0009-launch-without-consent.md)). One target process per debug session: launching while one is bound is `already-bound`, never an implicit kill. |

## Breakpoints (in `BreakpointTools`)

| Tool | Params | Notes |
|---|---|---|
| `set_breakpoint` | `sessionId`, `file`, `line` (1-based), `condition?`, `hitCount?`, `hitCountMode?` (`exact` default \| `every`), `logMessage?` | Returns id + bind state immediately; unbound carries a warning, never an error. `condition` is read-only (same rules as `evaluate`): break only when true. `hitCount` N breaks on the Nth hit (`exact`) or every Nth hit (`every`); `hitCountMode` alone is rejected. `logMessage` is a template with `{expression}` holes (each read-only): a logpoint never stops and never sets `lastHit`, and is incompatible with `condition`/`hitCount`. Bad options fail naming the input and create nothing. |
| `list_breakpoints` | `sessionId` | Poll until unbound flips to bound. Entries show `condition`, hit-count setting, current `HitCount`, and bind state; `Log` carries the session's buffered logpoint entries (readable without changing mode). Breakpoints the debug session created keep the `bp<N>` id `set_breakpoint` returned; every other breakpoint in the VS instance (set by the user or another debug session) is listed as `other<N>` (its current position, recomputed each call) with a warning, and is list-only. |
| `remove_breakpoint` | `sessionId`, `breakpointId` | Removes only breakpoints this debug session created. Ids are stable for the life of the debug session and never reused: removing one leaves every other id unchanged, and a removed id stays `breakpoint-not-found`. `other<N>` ids are rejected with `breakpoint-not-found`. |

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
| `read_variables` | `sessionId`, `frameId`, `path?` = null, `maxVariables` = 50 | Omit `path` for top level; pass an expandable `path` to drill into members. Both resolve in the frame named by `frameId` (unknown frame is `frame-not-found`, with or without `path`). `path` gets the same read-only rule as `evaluate`. Reading any frame leaves `evaluate` in `f0`. |
| `evaluate` | `sessionId`, `expression`, `timeoutMs` = 5000 | Read-only, always in the top frame (`f0`). Empty, `;` (statements), multiline, assignment, `++`/`--` and `<<=`/`>>=` are rejected client-side; `==`, `!=`, `<=`, `>=`, `=>` are fine. The live evaluator runs in no-side-effects mode, so a call that would mutate state is refused with `invalid-expression` ([ADR-0010](adr/0010-no-side-effects-eval.md)). |

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
| `vs-not-running` | no ROT instance, or override matched none, or the bound VS exited | start VS 2026 / check override / re-attach. Per-session: only the dead session is affected; `detach` it or re-attach. |
| `vs-busy` | VS rejected the call (RPC_E_CALL_REJECTED: modal dialog or blocked UI thread) | dismiss any modal dialog in VS and retry |
| `elevation-mismatch` | access denied talking to VS (integrity-level mismatch) | run bridge at the same elevation as VS (both elevated or both not) |
| `automation-failed` | other COM failure | dismiss dialogs and retry; restart VS if it persists |
| `timeout` | automation call exceeded its budget | dismiss dialogs and retry (with larger `timeoutMs` for waits) |
| `consent-required` | `attach_process` without `consent: true` | re-call with `consent: true` |
| `user-mismatch` | target process owned by another user (or owner unknowable — fails closed) | only attach to your own processes |
| `process-not-found` | no local process with that pid (exited or recycled) | re-list and retry promptly |
| `executable-not-found` | `launch_process` path has no file | pass the full path to a built target executable |
| `already-bound` | `launch_process` while the debug session already debugs a target process | detach or let the current run end, then launch |
| `launch-failed` | the OS could not start the executable, or the VS instance could not see the started target process | check the executable is runnable by the bridge user; retry |
| `not-in-break-mode` | inspect/step/evaluate outside break mode | resume to a breakpoint or pause first |
| `not-running` | `pause` while not running | resume first, then pause while running |
| `already-running` | `resume` while already running | pause first to inspect |
| `invalid-argument` | empty file/breakpointId/expression, bad line, bad step direction, `;`, newline, assignment, `++`/`--` or `<<=`/`>>=` in an expression or `read_variables` `path`, write-like/multiline `condition`, bad `hitCount`/`hitCountMode`, bad `logMessage` (empty, unbalanced braces, write-like hole, or combined with stopping options), relative, `devenv`, or non-.NET (no `.runtimeconfig.json` beside it) `executablePath`, missing `workingDirectory` | follow the per-field guidance in the message |
| `invalid-expression` | expression did not evaluate, or would have side effects (no-side-effects mode refused it) | pass a read-only expression valid in the top frame; prefer fields and property reads over method calls |
| `breakpoint-not-found` | unknown or already-removed breakpoint id, or an `other<N>` id (not created by this debug session) | call `list_breakpoints` for valid ids; remove `other<N>` breakpoints in VS |
| `frame-not-found` | unknown frame id | call `read_stack` for valid frame ids |
| `path-not-found` | unknown expandable path | drill down only via paths returned by `read_variables` |

`list_instances` never fails wholesale: unreachable/busy instances come back as
`reachable: false` entries with error text. `list_processes` skips processes
that exit mid-scan; unknown owners are excluded by default and flagged when the
filter is off.
