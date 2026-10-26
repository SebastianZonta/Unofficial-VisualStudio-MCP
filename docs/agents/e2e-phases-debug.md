# E2E phases P5-P9

Part of the E2E pass. Start at [e2e.md](e2e.md), then read this file only if it owns your slice.

## P5: inspection (`inspect`)

Set a breakpoint at `INSPECT`, launch `inspect` → `break`. Keep this target
stopped through W8 (writes run last: they change values the I cases read). Then remove the breakpoint and run to end.

| Id | Step | Expected |
|---|---|---|
| I1 | `read_stack` | ≥ 2 frames; `f0.function` contains `Inspect`; `module` set; `file`/`line` null |
| I2 | `read_stack(maxFrames: 1)` | 1 frame |
| I3 | `read_variables(f0)` | `order`, `counter`, `note`, `missing`, `subtotal` present; `order` expandable with a path; `missing` null; `note` value ends `[truncated]`, `expandable: true` |
| I4 | `read_variables(f0, maxVariables: 2)` | exactly 2 variables |
| I5 | `read_variables(f0, path: "order")` | members include `Customer`, `Items`, `Status`, `Total`, `Note` |
| I6 | `read_variables(f0, path: "order.Customer.Address")` | `City` = "Montevideo" |
| I7 | `read_variables(f0, path: "order.Nope")` | `path-not-found` |
| I8 | `evaluate` each of: `order.Id`, `order.Items.Count`, `order.Total`, `order.Note.Length`, `order.Status`, `order.Customer.Address.City`, `order.Coupon == null`, `order.Items[0].Sku` | 1001, 1500, 5625.00, 2000, Paid, "Montevideo", true, "SKU-0001"; each result's `expression` equals the input exactly (no `, nse`) |
| I9 | `evaluate("counter.Value")` | 0 |
| I10 | `evaluate("counter.Increment()")`, then `evaluate("counter.Value")` | `invalid-expression`; still 0 |
| I11 | `evaluate("order.Items.Clear()")`, then `evaluate("order.Items.Count")` | `invalid-expression`; still 1500 |
| I12 | `evaluate("counter.NextTicket")`, then `evaluate("counter.Value")` | first: OBS (record value or error); second: **0** (hard) |
| I13 | `read_variables(f0, path: "counter")`, then `evaluate("counter.Value")` | members listed; still **0** (a read must never run the side-effecting getter for real) |
| I14 | `evaluate("order.ComputeSubtotal()")`; `evaluate("order.Items.Count(x => x.Qty == 5)")` | OBS each (record value or error) |
| I15 | `evaluate` each of: `counter.Value++`, `--x`, `order.Id = 0`, `a <<= 1`, `a >>= 1`, `a; b`, two-line text | `invalid-argument` each; then `counter.Value` still 0 |
| I16 | `read_variables(f0, path: ...)` with each of `order.Id = 0`, `total++`, `a; b` | `invalid-argument` each |
| I17 | `read_variables(f0)` | top-level expandable `order` carries a non-empty `path`; `read_variables(f0, path: <that>)` lists members; `evaluate("order.Items[0].Sku")` equals the drill-down value (live drill path) |
| I18 | `evaluate("order.NoSuchField")`; a generic-arity call on a real generic member (else record OBS + gate UT as authority) | `invalid-expression` both |
| W1 | `set_variable(f0, "subtotal", "1.5")` (consent omitted); then `evaluate("subtotal")` | `consent-required`; still 5625.00 |
| W2 | `set_variable(f0, "subtotal", "1.5", consent: true)`; `read_variables(f0)` | echoes `frameId` f0, `path` subtotal, `type` decimal, `mode: break`, value 1.5; `subtotal` reads 1.5 |
| W3 | `set_variable` `note` = `"short"`; `missing` = `7`, then `missing` = `null` (consent each) | each echoes the new value; `evaluate("note.Length")` = 5, `evaluate("missing")` null |
| W4 | `set_variable(f0, "order.Id", "42", consent: true)`; `evaluate("order.Id")` | OBS (init-only: record value or `read-only-target`); if it errors, still 1001 |
| W5 | `set_variable(f0, "order.Items.Count", "1", consent: true)`; `evaluate("order.Items.Count")` | `read-only-target`; still 1500 |
| W6 | `set_variable` `subtotal` = `"abc"`; `1,5`; `order.Id` = `1.5`; `order` = `null` (consent each) | `type-mismatch` each naming expected vs provided; values unchanged |
| W7 | `set_variable(f0, "nope", "1", consent: true)`; `set_variable(f99, "subtotal", "1", consent: true)`; `set_variable` path `subtotal = 0` | `path-not-found` naming f0 and `nope`; `frame-not-found`; `invalid-argument` |
| W8 | `evaluate("subtotal = 0")` | `invalid-argument` (evaluate stays read-only) |
| W9 | `loop`: breakpoint at `TICK` with `condition: "i == 5"`, launch → `break`; `set_variable(f0, "total", "1000", consent: true)`; remove the breakpoint; `run_to_cursor` `AFTER_LOOP`; `evaluate("total")`; `resume` | write echoes 1000; at `AFTER_LOOP` `total` = 1200 (the write took effect in the running code); run ends `design` |
| W10 | design mode (no target): `set_variable(f0, "total", "1", consent: true)` | `not-in-break-mode` |

## P6: frames (`stack`)

Set a breakpoint at `STACK_BOTTOM`, launch `stack` → `break`.

| Id | Step | Expected |
|---|---|---|
| F1 | `read_stack` | `f0`…`f4` functions contain `Recurse`; a later frame contains `Stack` |
| F2 | `read_variables` for `f0`, `f2`, `f4` | `level` = 40, 20, 0 |
| F3 | `read_variables(f2, path: "node")` | `Depth` = 2, `Label` = "frame2" |
| F4 | then `evaluate("level")`; `evaluate("node.Depth")` | 40; 4 (evaluate stays in `f0` after reading `f2`) |
| F5 | `read_variables(f99)`; `read_variables(f99, path: "node")` | `frame-not-found` both |

Remove the breakpoint and run to end.

## P7: stepping (`step`)

Set a breakpoint at `STEP_START`, launch `step` → `break`.

| Id | Step | Expected |
|---|---|---|
| T1 | `evaluate("a")`, then `step(over)`, then `evaluate("a")` | 0, then `break` with `status: ok`, `lastHit` null, then 1 |
| T2 | `step(into)`; `read_stack` | `f0.function` contains `Add` |
| T3 | `step(out)`; `read_stack` | `f0.function` contains `Step` |
| T4 | `step(over)` until `evaluate("c")` = 4 (max 3 steps) | reached |
| T5 | `step(direction: "sideways")` | `invalid-argument` |

Remove the breakpoint and run to end.

## P8: execution control and launch

| Id | Step | Expected |
|---|---|---|
| E1 | `launch_process(Sandbox.exe, "wait 600")`, no breakpoints | `status: ok`, `mode: run`, a target PID |
| E2 | `step`, `read_stack`, `evaluate("1")` while running | `not-in-break-mode` each |
| E3 | `pause`; `pause` again | `break` ("paused on request"); then `not-running` |
| E4 | `resume`; `resume` again | `run`; then `already-running` |
| E5 | `pause`; `set_breakpoint` at `HEARTBEAT`; `resume` | `break`, `lastHit` ends `Scenarios.cs:<HEARTBEAT>`; `evaluate("beat")` ≥ 0 |
| E15 | still in E5's break: `list_modules(nameFilter: "SANDBOX")`; then `list_modules()` | `mode: break`; exactly one entry `Sandbox.dll`, `loaded: true`, `symbolState: loaded`, `symbolDetail` ends `Sandbox.pdb`. Unfiltered: also contains `System.Private.CoreLib.dll`, names sorted ascending |
| E6 | remove it; `resume(timeoutMs: 1000)`; `list_modules(nameFilter: "sandbox")` | `status: ok`, `mode: run` (still running is success); re-list works in run mode: `mode: run`, `Sandbox.dll` still `symbolState: loaded` |
| E16 | E1's target still running, no breakpoints: `run_to_cursor` at `HEARTBEAT`; `list_breakpoints` | `break`, `lastHit` ends `Scenarios.cs:<HEARTBEAT>`; no breakpoints listed (the temporary one is gone) |
| E17 | `set_breakpoint` at `HEARTBEAT` (id H); `run_to_cursor` at `HEARTBEAT`; `list_breakpoints` | `break` at `HEARTBEAT`; exactly one entry, id H (reused, not deleted). Remove H |
| E18 | `run_to_cursor` at `AFTER_LOOP` (never runs in `wait`), `timeoutMs: 1000`; `list_breakpoints` | `status: ok`, `mode: run`, `lastHit` null; no breakpoints listed |
| E27 | breakpoint at `HEARTBEAT`, `run_to_cursor HEARTBEAT` while a second concurrent `resume` is issued (or F5 in VS between set and continue) | `break` at `HEARTBEAT`, no `already-running` error, no leaked temp in `list_breakpoints` |
| E28 | user sets a breakpoint at the destination in VS during the `run_to_cursor` List–Set window (H-style manual step) | pre-existing id survives, `lastHit` is the destination |
| E19 | `run_to_cursor` with `line: 0`; with `file: ""` | `invalid-argument` both; nothing created |
| E7 | `resume(timeoutMs: 999)`; `resume(timeoutMs: 60001)` | `invalid-argument` both |
| E8 | `launch_process` again while E1's target runs | `already-bound` |
| E9 | `Stop-Process` the E1 target; `list_breakpoints`; `list_modules` | `mode: design`; `not-running` (not debugging) |
| E20 | still in design after E9: `run_to_cursor` at `HEARTBEAT`; `list_breakpoints` | `not-running`; no breakpoints listed |
| E10 | `launch_process(Sandbox.exe, "exit")` | ends `mode: design` (note says the run ended) |
| E11 | Breakpoint at `ECHO`; `launch_process(Sandbox.exe, arguments: "echo a1 \"two words\"", workingDirectory: %TEMP% (absolute), environment: {SANDBOX_E2E: "yes"})` | `break`; `argsJoined` = "a1\|two words"; `cwd` = %TEMP% (case-insensitive, no trailing slash); `envValue` = "yes". Remove it, run to end |
| E12 | Breakpoint at `ASYNC`; launch `async` | `break`; `f0.function` contains `AsyncFlow`; `before`, `after` are integers. Remove it, run to end |
| E13 | launch `throw` (no breakpoints) | `break`, `lastHit` null; a frame contains `Fail`; `evaluate("$exception.Message")` = "sandbox boom". `exception.type` = `System.InvalidOperationException`, `exception.path` = `$exception`, `exception.message` equals that `evaluate` value exactly (OBS if null: record the note). `resume` (repeat once if still `break`) → `design` |
| E21 | design mode: `list_exception_settings(nameFilter: "invalidoperation")`; `list_exception_settings()` | `mode: design`; contains `System.InvalidOperationException`, record its mode as M0; unfiltered list sorted by name (OBS: entry count) |
| E22 | `set_exception_setting("System.InvalidOperationException", "first-chance")`; launch `throw` | set echoes `first-chance`; launch → `break`, note starts `first-chance`, `exception.type` = `System.InvalidOperationException`, `exception.message` equals `evaluate("$exception.Message")`, `lastHit` null. `resume` → OBS (record whether a second `unhandled` stop comes); resume to `design` |
| E23 | launch `wait 600`; `list_exception_settings(nameFilter: "invalidoperation")`; `set_exception_setting("System.InvalidOperationException", "unhandled-only")`; `Stop-Process` it | list works: `mode: run`, still `first-chance`; set → `already-running` naming run; setting unchanged |
| E24 | `set_exception_setting` with `"InvalidOperationException"`; `"System.NoSuchException"`; then `mode: "always"`; `exceptionType: ""` | `exception-not-found`; `exception-not-found`; `invalid-argument`; `invalid-argument` |
| E25 | `set_exception_setting("System.InvalidOperationException", "default")`; list it | mode back to M0. Cleanup: this must run even if E22–E24 failed |
| E14 | `launch_process` with: relative `Sandbox.exe`; absolute missing `C:\nope\Sandbox.exe`; the devenv path; `C:\Windows\System32\notepad.exe`; valid exe with `workingDirectory: C:\nope` | `invalid-argument`; `executable-not-found`; `invalid-argument`; `invalid-argument`; `invalid-argument`. No process starts (nothing new named Sandbox in `list_processes`) |

## P9: attach_process (`waitPid` from S4)

| Id | Step | Expected |
|---|---|---|
| A1 | `attach_process(waitPid, consent: false)` | `consent-required` |
| A2 | `attach_process(999999, consent: true)`; `attach_process(0, consent: true)` | `process-not-found`; `invalid-argument` |
| A3 | `attach_process(waitPid, consent: true)` | `status: ok`, `mode: run` |
| A4 | `launch_process(Sandbox.exe, "exit")` | `already-bound` (target PID = `waitPid`) |
| A5 | `pause`; `read_stack` | `break`; ≥ 1 frame |
| A6 | `set_breakpoint` at `HEARTBEAT`; `resume` | `break`, `lastHit` ends `Scenarios.cs:<HEARTBEAT>` |
| A7 | remove it; `Stop-Process waitPid`; `list_breakpoints` | `mode: design` |
| E26 | `attach_process(<vsPidA>, consent: true)`; `list_processes` | `invalid-argument` naming devenv (no self-attach); `list_processes` still lists the VS process |

A `list_instances` PID (devenv) is refused by `attach_process` (E26) — never
attach to a process you did not start. The user-mismatch path stays with the unit
tests: attaching to a foreign-owned process isn't safe to try here.
