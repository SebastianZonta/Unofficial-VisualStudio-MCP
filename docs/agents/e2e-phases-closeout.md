# E2E phases P10-P11, H, audit medium

Part of the E2E pass. Start at [e2e.md](e2e.md), then read this file only if it owns your slice.

## P10: two VS instances

| Id | Step | Expected |
|---|---|---|
| M0 | Start VS B on `%TEMP%\SandboxB\Sandbox.slnx` (as in S2/S3); record `vsPidB` and display name `B` | reachable |
| M1 | `attach()` with no override | binds the newest instance: `list_sessions` shows the new session on `B`. Record it as SB. Parallel run: the newest instance belongs to another runner — detach immediately with no calls under it and record M1 PASS-with-note (the contract held) |
| M2 | `attach(B)` | same SB |
| M3 | `list_sessions` | S on `A` and SB on `B` |
| M4 | S: breakpoint at `STACK_BOTTOM`, launch `stack` → `break`. SB: breakpoint at the copy's `INSPECT`, launch the copy's exe `inspect` → `break` | both break |
| M5 | `read_stack(S)`; `read_stack(SB)` | `f0` contains `Recurse`; `f0` contains `Inspect` |
| M6 | `list_breakpoints(S)`; `list_breakpoints(SB)` | each shows only its own file (`Sandbox\` vs `SandboxB\`); no `other<N>` entries |
| M7 | `evaluate(S, "level")`; `evaluate(SB, "order.Id")` | 40; 1001 |
| M8 | `Stop-Process` SB's target, then `Stop-Process vsPidB`; `list_breakpoints(SB)`; `read_stack(S)` | SB: `vs-not-running`; S: still `break`, same frames |
| M9 | `detach(SB)`; `list_sessions`; `attach(B)` | detach echoes SB; only S remains; `vs-not-running` |

Remove S's breakpoint and run S to end.

## P11: cleanup (always run, even after a FAIL or BLOCKED)

| Id | Step | Expected |
|---|---|---|
| Z1 | Set one breakpoint at `TICK`; `detach(S)`; `attach(A)` (fresh session); `list_breakpoints` | detach deleted it: no entry at any `Sandbox` path, not even as `other<N>` |
| Z2 | `detach` the Z1 session; `list_sessions` | no session on `A` or `B` |
| Z3 | `Stop-Process` every Sandbox process and VS instance this pass started (`waitPid`, launched targets, `vsPidA`, `vsPidB`), and only those; delete `%TEMP%\SandboxB` | `list_processes(nameFilter: "sandbox")` empty |

## H: human-assisted (main agent, after the subagent returns)

Only a person can set or delete a breakpoint in VS outside a debug session, so
the main agent runs these with the user. It asks before each manual step
(AskUserQuestion) and waits for confirmation. Start VS A again as in S2/S3,
`attach(A)` → SH, and use the same report format (append to the run's file).

| Id | Step | Expected |
|---|---|---|
| H1 | SH: `set_breakpoint` at `TICK` → id P. User: in VS A, click the gutter to add a breakpoint at the `ECHO` line. Then `list_breakpoints` | P listed once under its id; the `ECHO` breakpoint listed once as `other<N>`, with the warning that it was not created by this debug session |
| H1b | SH: `set_breakpoint` at the `ECHO` line (the user's). User confirms it is still in the VS Breakpoints window | `breakpoint-exists` naming H1's `other<N>`; still there, unchanged |
| H2 | `remove_breakpoint(<that other<N>>)`. User confirms it is still in the VS Breakpoints window | `breakpoint-not-found`; still there |
| H3 | User: delete breakpoint P by hand in VS. Then `list_breakpoints`, `read_stack` | P gone from the list; session healthy (`not-in-break-mode` from `read_stack` is fine; `vs-not-running` is a FAIL) |
| H4 | `remove_breakpoint(P)` | `breakpoint-not-found` |
| H5 | SH: `set_breakpoint` at `AFTER_LOOP`; `detach(SH)`. User checks the VS Breakpoints window | only the user's `ECHO` breakpoint remains (detach removed only what the session created). User deletes it, then the main agent stops VS A |

## Audit medium (E29–E31, T6, I19, W11, E25b)

Manual cases for the medium audit (`.scratch/audit-oct24-medium/spec.md`); the automated
suite covers the bridge parity subset in `MediumAudit_BridgeParity`, the rest stay manual.

| Id | Step | Expected |
|---|---|---|
| E29 | `attach` to the Sandbox managed target (P9 `waitPid` with `consent: true`) | breakpoints bind and stop at entry (not ntdll loader); proves managed-engine attach. Native-only is a known limit |
| T6 | `step(over)` from `STEP_START` landing on a breakpoint | identical `lastHit` file:line on fast and over-budget paths (force the latter once with a tiny `timeoutMs`) |
| I19 | drill `order.Items[0].Sku` via the returned `path` with no manual edit; `read_variables(f0)` shows no `File`/`Line` on frames; `evaluate("order.Nope")` | drill value equals `evaluate("order.Items[0].Sku")`; `invalid-expression` |
| W11 | `set_variable(f0, "note", "32 ' '")`-shaped char round-trip on a real char local (or Sandbox `note` string analogue) | echoes the glyph; refused-write and disconnect probes stay UT-only unless reproducible live |
| E30 | `launch_process` with `workingDirectory: ""` / missing dir / `=` env key | `invalid-argument` each, no process starts; stale-publish placeholder probe stays UT-only |
| E31 | `list_modules` asserts only `loaded`/`missing`/`unknown`, never `failed`; `list_sessions` omits dead sessions after `Stop-Process vsPid` (M8 pattern) | states hold; recycled-PID guard stays UT-only (`expectedProcessName` path) unless a short-lived target is scripted |
| E25b | kill the target mid-restore window (extends E25) | restore reports failure, `detach` still runs, no leaked session |
