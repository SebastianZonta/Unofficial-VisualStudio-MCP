# E2E two-VS regression

Run after implementing a feature, before opening a PR. Reduces regression
risk; it proves nothing 100% — unit tests (`dotnet test VsDebugMcp.slnx --no-build`)
plus this pass are the bar.

## Who runs this

Fan out this pass to a subagent running against the freshly published
binary. The subagent walks sections 1–7 in order, exercising every tool
across both instances at once: the full debug loop on A while proving B
stays isolated and usable throughout.

Done when: every section reaches its "Done when" — perfection is the bar:
every tool flawless on both instances; a single tool failure fails the pass.

## Prerequisites

- Two VS 2026 instances open, same Windows user and elevation, with two
  distinct Solutions from two separate folders: A = DemoApi
  (`http://localhost:5070`), B = copy DemoApi2 (`http://localhost:5071`).
  One Solution per VS instance; never open the same folder/solution in
  both instances (shared `file:line` causes breakpoint collision and
  orphaned breakpoints). Dismiss all modal dialogs.
- `dotnet build VsDebugMcp.slnx` green. Note the two display names from ROT;
  you need both exact strings.

## 1. Session-less discovery (no session yet)

1. `list_instances` → exactly 2 entries, both `reachable: true`, solutions A and B correct.
2. `list_sessions` → empty (or only pre-existing sessions you can name).
3. `list_processes` (defaults) → target program present with `mine: true`.
   Never use a `list_instances` PID as an attach target — VS PID (`devenv`)
   vs target PID confusion is the classic false failure.

Done when: 2 reachable instances, target process `mine: true`.

## 2. Attach both, prove isolation

1. `attach` (omit override) → binds newest instance; record `sessionId-A`.
2. `attach` with `instanceOverride` = other display name → `sessionId-B`.
3. `list_sessions` → both sessions listed.
4. Re-`attach` with A's override → returns the *same* `sessionId-A`
   (one live session per VS instance).

Done when: two distinct session ids, re-attach is idempotent, `list_sessions` shows both.

## 3. Breakpoints on A, silence on B

1. On A: `set_breakpoint(file, line)` → id returned; unbound is a warning, not an error.
2. `list_breakpoints(A)` → contains the id; poll until `bound: true`.
3. `list_breakpoints(B)` → empty (no leakage across sessions).
   Distinct Solutions/target processes keep `file:line` distinct, so B stays
   empty and no stray breakpoints survive the run.

Done when: A bound, B empty.

## 4. Full debug loop on A (exercises every execution/inspection tool)

1. `attach_process(A, targetPid, consent: true)` → `mode: run`.
   Spot-check the gate once: `attach_process` without `consent: true` on B
   must return `consent-required` (do not repeat for every session).
2. Trigger the breakpoint (exercise the target or `resume` with a generous
   `timeoutMs`; `timeout` status means retry, never a hang).
3. On break: result carries `lastHit: "<file>:<line>"`. (`lastHit: null`
   on step/exception breaks is normal.)
4. `read_stack(A)` → ≥1 frame, `function` + `module` present (no file/line —
   EnvDTE exposes none).
5. `read_variables(A, frame0)` → values sane; if a value is `expandable`,
   drill once via its `path` (covers truncation/`[truncated]` path).
6. `evaluate(A, "<simple read-only expr>")` → value matches locals.
   No assignments, statements, or `;` — `evaluate` is read-only.
7. `step(A, over)` → stays in `break` (or moves `lastHit` to null on step — normal).
8. `pause` while running → `break`; `resume` → `run` then break on next hit.
   Spot-check one negative once: `step` in `run` mode → `not-in-break-mode`.

Done when: attach → break (`lastHit` set) → stack → variables (+1 drill-down)
→ evaluate → step → pause/resume all succeed on A.

## 5. Conditional breakpoint types on A

A is still attached from §4 with the debuggee running. Trigger hits with
an HTTP `GET /work?n=<k>` against DemoApi (`/ping` answers 200 when idle).
Use a stable line (previously Program.cs:9); remove each breakpoint before
setting the next.

1. `condition` false (e.g. `n == -1`) → trigger → must NOT stop (`resume`
   returns `run`, `lastHit` null). `condition` true (e.g. `n == 5`, trigger
   with `?n=5`) → breaks with `lastHit` set. Conditions are read-only:
   a write-like condition (e.g. `n = 5`) is rejected with
   `invalid-argument` and creates nothing.
2. `hitCount: 2` (exact is default) → trigger twice → breaks on the 2nd
   hit only. `hitCount` + `hitCountMode: every` → breaks every Nth hit.
   (Cosmetic: VS reads 0 hits on the stopping hit — display-only.)
3. Combined `condition` + `hitCount` → breaks only when the condition holds
   on the Nth hit.
4. `logMessage` (e.g. `"n is {n}"`) → trigger → must NEVER stop; exactly one
   entry per trigger is retrievable from the session log carried by
   `list_breakpoints`. Drain each trigger (resume → confirm the entry)
   before firing the next; never remove/re-set the breakpoint mid-case.
   Regression: the spec keys on the VS snapshot path, so a user-passed
   relative `file` still matches `lastHit`; the resume path waits for the
   Go to take effect, so a slow thread is never logged twice.
5. Invalid inputs are rejected with `invalid-argument` naming the field and
   create nothing: `logMessage` combined with `condition`/`hitCount`,
   empty or unbalanced-brace `logMessage`, non-positive `hitCount`.

Done when: false-condition never stops, true-condition stops with `lastHit`,
exact breaks on the Nth hit, every breaks every Nth, combined needs both,
logpoint never stops and logs, invalid inputs rejected without creating.

## 6. Sibling-session isolation under load

While A sits in `break` (leave one §5 breakpoint active):

1. Every B call still works: `list_breakpoints(B)`, `read_stack(B)` fails
   only with `not-in-break-mode` (B is in `run`/`design`, never A's data).
2. `detach(B)` → echoes B's id; A unaffected (still `break`, breakpoints intact).
3. Re-`attach` B's override → fresh or same id, `list_sessions` consistent.

Done when: B usable while A is stopped; `detach` affects only its own session.

## 7. Cleanup (required — leaves no state)

1. `remove_breakpoint` every A id from §§3 and 5 → `list_breakpoints(A)` empty.
2. `detach(A)`, `detach(B)` → `list_sessions` empty.
3. Unknown-id spot-check once: `detach("nope")` or
   `remove_breakpoint(A, "nope")` → `no-session` / `breakpoint-not-found`,
   proving error mapping still works after your change.

## Pass criteria

- `dotnet test VsDebugMcp.slnx --no-build` green.
- Sections 1–7 all reach their "Done when" with zero `vs-busy`,
  `elevation-mismatch`, or `vs-not-running` surprises (dismiss dialogs and
  retry once before calling it a regression).
- On any failure: record failing tool + `code` + which session (A/B) and
  stop — do not "fix" by skipping sections.
