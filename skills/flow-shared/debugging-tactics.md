# Debugging tactics (shared)

Read this file first. Terms below are `GLOSSARY.md` terms: use them exactly (`VS instance` vs `debug session` vs `target process`, `VS PID vs target PID`, `mode`/`lastHit`, `mine`/`consent`, `foreign breakpoint`).

## 1. No blind breakpoint

Before any `set_breakpoint`/`evaluate`, read the surroundings: the target method, its caller(s), and what computes the values you will read. Anchor by surrounding code, never by a bare line number. A breakpoint stops before its line runs: never read a declaration-with-initializer's variable on its own line (it still holds default) — stop on the next executable line instead. One breakpoint at a time; confirm it binds via `list_breakpoints` before adding the next.

## 2. Bind order

`list_instances` → `attach` (with the entry's display name as `instanceOverride`; omit only for newest VS) → keep the `sessionId` → `list_processes(nameFilter)` / `launch_process` (absolute exe path, never devenv). `list_instances` PIDs are VS PIDs (for `attach` only); `list_processes` PIDs are target PIDs (for `attach_process` only — `consent: true` required, `expectedProcessName` optional but recommended as a recycled-PID guard). `mine: true` entries only; unknown owners fail closed. `detach` frees the binding and deletes owned `bp<N>`, never the target.

## 3. Breakpoint discipline

`set_breakpoint(sessionId, file, line)` returns immediately — `unbound` is a warning, never an error (it never throws `unbound-location`; only `run_to_cursor` to an unbound line does). Same location + same options returns the same `bp<N>`; different options is `invalid-argument` (remove first, then set). A location owned by the user/another session is `breakpoint-exists` naming `other<N>`: list-only, never `remove_breakpoint` it. `other<N>` is positional per `list_breakpoints` — do not store it across calls. A breakpoint that never flips from `unbound`: first check `list_modules(nameFilter)` — absent module or `symbolState` not `loaded` (`loaded`/`missing`/`unknown`; there is no `failed` state) explains it. `run_to_cursor(sessionId, file, line)` uses a temporary breakpoint: only a newly created one is removed on the way out and never kept as `bp<N>`; reusing an existing breakpoint at the destination leaves it listed (and when VS stays busy/dies the id stays owned for a retry). An earlier hit wins as `lastHit`. `remove_breakpoint` takes only an owned `bp<N>`.

## 4. Mode table

Every result echoes `mode` (`design`/`run`/`break`) and `lastHit` (`file:line` or null). Inspect only in `break`: `read_stack`, `read_variables`, `evaluate`, `set_variable`. `pause` needs `run` (`design` → `not-running`); `resume` needs `break` or `design` (`run` → `already-running`); `set_exception_setting` needs `break`/`design`, never `run` (`list_exception_settings` is read-only in any mode). `already-bound` means the session already owns a target — end the current run (or `detach`) before launching/attaching again. `launch_process` starts suspended-attached-resumed, so pre-launch breakpoints bind at startup; wait for the first break up to `timeoutMs`.

## 5. Inspect

`read_stack(sessionId)` (newest first) → `read_variables(sessionId, frameId)` for that frame's top level → drill `expandable.path` via `read_variables(path=...)`. `evaluate(sessionId, expression)` runs in the top frame (`f0`) only, read-only: `=`/`++`/`--`/`;`/multiline/`//`/`/* */`/calls with side effects are rejected (`invalid-expression`/`invalid-argument`); re-read to confirm (`counter.Value` must not move). It returns `Expandable` only (no `path`); drill via `read_variables(path=...)`. `path` must come from a prior `read_variables` result or it is `path-not-found`. Values keep the first 500 chars plus ` [truncated]` and then read as expandable. `set_variable` needs `consent: true` on every call plus a confirming `evaluate`; init-only/computed targets are `read-only-target`.

## 6. Stuck → recovery

- `no-session` / stale `sessionId` (after `detach`): `list_sessions` → `attach` again → retry with the new id.
- `not-in-break-mode` on inspect/step: `pause` → act in `break` → `resume` to run.
- Devenv PID passed to `attach_process`: stop; `list_instances` = VS, `list_processes` = targets.
- `breakpoint-exists` (`other<N>`): use as-is or delete in VS; never claim it.
- `unbound` that never flips: `list_modules` symbol check before moving the line.
- `timeout` status (over budget): retry with a larger `timeoutMs` (backoff up to ~5x; temp-breakpoint removal retries 4x); never a verdict. `vs-busy` (modal dialog): dismiss, retry ≤5x. `vs-not-running` (VS killed): `list_instances` → `attach` again.
- Logpoint (`logMessage` with `{expression}` holes): never stops, never sets `lastHit`; read its output via `list_breakpoints` `Log`. Incompatible with `condition`/`hitCount`/`hitCountMode`. `condition` = break-when-true; `hitCount` N + `exact` (Nth hit) / `every` (every Nth). `condition` / plan `Stop when`: field/property comparison only — no method calls, no `Contains`/LINQ/`()`; grep for `(` before returning and rewrite violations as an unconditional breakpoint plus a manual `evaluate` filter. Flow runs are the exception: runner Budgets retries `resume`/`run_to_cursor` once only.
- Exception break result carries `exception` (`type`, `path` `$exception`, `message`) and no `lastHit`; `[Exception]` frames in `read_stack` are the after-the-fact view.

## 7. Budgets

Defaults: `resume`/`run_to_cursor` 5000, `launch_process` 10000, `attach_process` 30000. `timeoutMs` must be 1000–60000; `maxFrames` 1–200; `maxVariables` 1–500. `launch_process` runs once (a timeout may leave the target running — treat as a stop).

## 8. Evidence

Quote `mode` + `lastHit` at every stop and the `code: detail` of every error. A logpoint never sets `lastHit` — say so when citing it.
