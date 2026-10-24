# E2E: every tool against a real VS, on the Sandbox

The unit tests prove the tool contracts against `FakeDebugBridge`. This pass
proves them against real VS 2026: every one of the 21 tools, every reachable
error code, and the situations the fake can't produce (COM identity, real
frames, the evaluator, threads, exceptions, two VS instances). The target is
`Sandbox/`, a .NET console app built for this pass. The pass starts its own VS
instances and never touches any other VS instance.

Bar: every non-OBS case PASS. One FAIL or BLOCKED fails the pass.

Contents: [Automated suite](#automated-suite-audit-should-fix) · [Roles](#roles) · [Runner rules](#runner-rules) · [Sandbox reference](#sandbox-reference) · cases [P0 setup](#p0-setup) → [P1 discovery](#p1-discovery-session-less) → [P2 lifecycle](#p2-session-lifecycle) → [P3 breakpoint identity](#p3-breakpoint-identity-and-ownership) → [P4 conditions](#p4-conditions-hit-counts-logpoints-loop) → [P5 inspection](#p5-inspection-inspect) → [P6 frames](#p6-frames-stack) → [P7 stepping](#p7-stepping-step) → [P8 execution and launch](#p8-execution-control-and-launch) → [P9 attach_process](#p9-attach_process-waitpid-from-s4) → [P10 two VS](#p10-two-vs-instances) → [P11 cleanup](#p11-cleanup-always-run-even-after-a-fail-or-blocked) · [H human-assisted](#h-human-assisted-main-agent-after-the-subagent-returns) · [Report](#report)

## Automated suite (audit-should-fix)

`tests/VsDebugMcp.E2E` drives the same Sandbox target through the same tools over
MCP stdio against the real server binary in live mode: one case per audit
behaviour (stable ids, frame-true inspection, read-only gate incl.
no-side-effects, no ghost resume, parallel attach, evaluate budget, attach mode,
one-target rule). Opt-in and VS-free by default:

```powershell
$env:VSDEBUG_E2E=1
$env:VSDEBUG_E2E_VS_A="!VisualStudio.DTE.18.0:<pidA>"  # from list_instances
$env:VSDEBUG_E2E_VS_B="!VisualStudio.DTE.18.0:<pidB>"  # optional: enables the two-VS cases
dotnet test tests/VsDebugMcp.E2E/VsDebugMcp.E2E.csproj
```

It never runs in CI. Busy-VS behaviour (a modal dialog open in VS) stays a manual
step: it is the `BusyVs_StillSurfacesAfterRetryWindow` case, marked manual in the
suite. The manual pass below remains the authority for everything the suite skips.

## Roles

**Main agent (orchestrator)** does preflight, fans out, then triages:

1. `dotnet build VsDebugMcp.slnx` and `dotnet test VsDebugMcp.slnx --no-build` green.
2. Publish the MCP and get the host restarted on the fresh binary (live is the
   default; `--live` is accepted as a no-op)
   (stop the clients first: the exe is locked while hosts run). Restarting the
   host is the user's action; ask, don't work around it.
3. Check the host: the vs-debug toolset must list all 21 tools (`run_to_cursor`,
   `list_exception_settings`, `set_exception_setting` included). If any tool is missing, the host runs a stale binary: stop.
4. Fan out exactly one subagent on **muse-spark-1.3-contributor** (opencode) / **sonnet** (Claude Code), using the prompt below.
5. After it returns, run phase **H** (human-assisted) yourself with the user.
6. Read the report. Triage every FAIL and BLOCKED into `.scratch/` issues, and
   relay the verdict to the user. A FAIL is never "fixed" by editing this doc
   to match the observed behaviour without saying so.

```text
Agent(
  description: "E2E against Sandbox",
  subagent_type: "general-purpose",
  model: "opencode-go/muse-spark-1.3-contributor",  # Claude Code: model: "sonnet"
  prompt: """
    You are the E2E runner for the VS debug MCP at <repo root>.
    Read docs/agents/e2e.md fully, then run phases P0-P11 in order, exactly as written.
    Load the vs-debug MCP tools via ToolSearch (query "+vs-debug", max_results 20).
    Never touch a VS instance you did not start. Never skip a case silently.
    Write the report where the doc says and return: verdict, PASS/FAIL/BLOCKED/OBS
    counts, every FAIL and BLOCKED with evidence, and the report path.
    Change under test: <one line>. Extra focus: <optional>.
  """)
```

## Runner rules

- **Case ids.** Report every case by its id. Statuses: PASS, FAIL (actual
  differs from expected), BLOCKED (couldn't run; give the reason), OBS
  (observation: record what happened; no expected value).
- **Evidence.** For each case, quote the relevant fields of the tool result (or
  the error `code: detail`).
- **Retries.** Retry once on `vs-busy` or a `timeout` status before calling it a
  FAIL. Never retry an assertion until it passes.
- **Isolation.** Every `attach` passes `instanceOverride` (the display name of an
  instance you started). The only exception is M1, which targets the newest
  instance on purpose. Act on VS only through MCP tools. The shell is for building, starting
  and stopping the processes this pass owns, nothing else.
- **Budgets.** Use `timeoutMs` 30000 for `launch_process`/`resume` unless the
  case says otherwise. "Run to end" means `resume` until `mode: design`.
- **Values.** VS renders strings with quotes (`"Montevideo"`) and decimals
  with their scale (`5625.00`, or `5625.00M`). Compare the value, not the
  decoration.
- **Bound target.** One target process per debug session. Before each
  `launch_process`, the previous target must have exited (`mode: design`).

## Sandbox reference

Build: `dotnet build Sandbox/Sandbox.csproj -c Debug`. Exe:
`<repo>\Sandbox\bin\Debug\net10.0\Sandbox.exe <scenario> [n]`. Pass breakpoint
files as absolute paths. Resolve each marker's line in P0 (search
`Sandbox/Scenarios.cs` for `// E2E:<NAME>`) and use those numbers. Never
hardcode line numbers.

| Marker | Scenario | In scope at the marker |
|---|---|---|
| `TICK` | `loop [n=20]` | `i` (1..n, one hit each), `total` (sum of 1..i-1) |
| `AFTER_LOOP` | `loop` | `total` (210 for n=20) |
| `INSPECT` | `inspect` | `order`, `counter`, `note`, `missing`, `subtotal` (see below) |
| `STACK_BOTTOM` | `stack [n=5]` | per frame `depth`, `level` = depth*10, `node` (`Depth`, `Label`="frame<depth>"); bottom frame depth 4 |
| `STEP_START` / `STEP_CALL` / `STEP_INTO` | `step` | `a`, `b`, `c`; `STEP_INTO` is inside `Add(x, y)` (`sum`) |
| `WORKER` | `threads [n=4]` | `id` (1..n), `n` (1..5): 20 hits for n=4 |
| `ASYNC` | `async` | `before`, `after` (thread ids) |
| `THROW` | `throw` | unhandled `InvalidOperationException("sandbox boom")` from `Fail(reason)` |
| `ECHO` | `echo <args...>` | `argsJoined` (args after the scenario joined by `\|`), `cwd`, `envValue` (`SANDBOX_E2E` or `(unset)`) |
| `HEARTBEAT` | `wait [seconds=600]` | `beat`, `next`; hit every 250 ms; exits by itself after `seconds` |
| (none) | `exit` | exits immediately with code 3 |

`inspect` facts: `order.Id` 1001; `order.Status` Paid; `order.Customer.Address.City`
"Montevideo"; `order.Items.Count` 1500; `order.Total` and `subtotal` 5625.00;
`order.Note.Length` 2000; `order.Tags.Count` 2; `order.Coupon` and `missing`
null; `counter.Value` 0. `counter.Increment()` and the getter
`counter.NextTicket` both bump `Value`, so `counter.Value` must still read 0
after every inspect case.

## P0: setup

| Id | Step | Expected |
|---|---|---|
| S1 | Build the Sandbox; resolve every marker line | build green; 12 markers found |
| S2 | Resolve devenv: `& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property productPath`; start VS A: `Start-Process <devenv> '"<repo>\Sandbox\Sandbox.slnx"' -PassThru`; record `vsPidA` | process started |
| S3 | Poll `list_instances` every 10 s (max 3 min) | an entry with pid `vsPidA`, `reachable: true`, solution `Sandbox`; record its display name `A` |
| S4 | Start `Sandbox.exe wait 900` from the shell (not under a debugger); record `waitPid` | prints its pid |
| S5 | Copy `Sandbox/` (without `bin/`, `obj/`) to `%TEMP%\SandboxB` and build it there | build green; distinct file paths for VS B |

If S2/S3 fail, mark every later case BLOCKED and go to P11.

## P1: discovery (session-less)

| Id | Step | Expected |
|---|---|---|
| D1 | `list_instances` | contains `A` (reachable, solution path ends `Sandbox.slnx`). Other instances may appear: ignore them |
| D2 | `list_sessions` | no session bound to `A` |
| D3 | `list_processes(nameFilter: "sandbox")` | exactly the `waitPid` entry (case-insensitive filter), `mine: true` |
| D4 | `list_processes(ownedOnly: false)` | at least as many entries as `list_processes()`; any `mine: false` entry has an owner or error text |

## P2: session lifecycle

| Id | Step | Expected |
|---|---|---|
| L1 | `attach(A)` | `sessionId` S, `mode: design` |
| L2 | `attach(A)` again | same S |
| L3 | `list_sessions` | S listed with display name `A` |
| L4 | `detach(S)`, then `attach(A)` | detach echoes S; the new id S2 differs from S. Use S2 as **S** from here |
| L5 | `detach("nope")`; `list_breakpoints("nope")` | `no-session` both |
| L6 | `list_breakpoints(S, ...)` with empty `sessionId` `""` | `invalid-argument` |

## P3: breakpoint identity and ownership

Use `file` = absolute `Sandbox\Scenarios.cs`. Breakpoints set by hand in VS
are covered in phase H.

| Id | Step | Expected |
|---|---|---|
| B1 | `set_breakpoint` at `AFTER_LOOP`, `TICK`, `HEARTBEAT` | ids `bp<N>` X, Y, Z, all distinct, each `bound: true` |
| B2 | `remove_breakpoint(X)`, then `remove_breakpoint(Z)` | each echoes its own id |
| B3 | `list_breakpoints` | exactly one entry: id Y at the `TICK` line (no `other<N>` twin of a session breakpoint) |
| B4 | `remove_breakpoint(X)` again | `breakpoint-not-found` |
| B5 | `set_breakpoint` at `AFTER_LOOP` | a new id that is none of X, Y, Z; `list_breakpoints` shows Y and it, each exactly once |
| B6 | `remove_breakpoint(S, "other1")` | `breakpoint-not-found` (remediation names the user or another debug session) |
| B7 | `remove_breakpoint` B5's id and Y; `list_breakpoints` | both echo; no breakpoints at all |
| B8 | `remove_breakpoint(S, "")` | `invalid-argument` |
| B9 | `set_breakpoint(file: "", ...)`; `set_breakpoint(line: 0)` | `invalid-argument` both; nothing created |
| B10 | `set_breakpoint` at line 1 of `C:\does\not\exist.cs`; then remove it | OBS: record `bound`/`warning` or the error |
| B11 | `set_breakpoint` at `TICK` twice; then again with `hitCount: 3`; remove it | same id both times, `list_breakpoints` shows it once; `invalid-argument` naming that id; removed |

## P4: conditions, hit counts, logpoints (`loop`)

Each case: set the breakpoint(s), `launch_process(Sandbox.exe, "loop")`, check,
remove the case's breakpoints, run to end. `list_breakpoints` must be empty
before the next case.

| Id | Breakpoint at `TICK` | Expected |
|---|---|---|
| C1 | `condition: "i == 7"` | launch → `break`, `lastHit` ends `Scenarios.cs:<TICK>`; `evaluate("i")` = 7; `evaluate("total")` = 21 |
| C2 | `condition: "i == -1"` | never breaks; ends `design` |
| C3 | `hitCount: 3` (exact) | breaks once, `i` = 3; resume → `design` (no second stop) |
| C4 | `hitCount: 4, hitCountMode: "every"` | breaks at `i` = 4, then (resume) `i` = 8 |
| C5 | `condition: "i >= 5", hitCount: 2` | breaks at `i` = 6 (the hit count counts only hits where the condition holds). Any other `i` is a FAIL: record it, because TOOLS.md or the code is wrong |
| C6 | `logMessage: "i={i} total={total}"` | launch never stops; ends `design`. `list_breakpoints` `Log` has exactly 20 entries `i=1 total=0` … `i=20 total=190` in order, each `breakpointId` = the logpoint's id, `hitCount` increasing |
| C7 | `condition: "n++ > 0"`; `logMessage: "i={i++}"`; `condition: "i = 5"` | `invalid-argument` each, naming condition/logMessage; `list_breakpoints` empty |
| C8 | `logMessage` + `condition`; `logMessage: ""`; `logMessage: "a{b"`; `hitCount: 0`; `hitCountMode: "every"` alone; `hitCountMode: "sometimes"` with `hitCount: 2` | `invalid-argument` each, naming the field; nothing created |
| C9 | `condition: "i == 3 \|\| i == 4"` | breaks at `i` = 3, then `i` = 4 (comparison operators pass the read-only rule) |
| C10 | `threads` scenario: logpoint at `WORKER` `"{id}/{n}"`; `launch_process(Sandbox.exe, "threads")` | never stops; ends `design`; `Log` has exactly 20 entries, each pair `1/1` … `4/5` exactly once |

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

Never pass a `list_instances` PID (devenv) to `attach_process`, and never attach
to a process you did not start. The user-mismatch path stays with the unit
tests: attaching to a foreign-owned process isn't safe to try here.

## P10: two VS instances

| Id | Step | Expected |
|---|---|---|
| M0 | Start VS B on `%TEMP%\SandboxB\Sandbox.slnx` (as in S2/S3); record `vsPidB` and display name `B` | reachable |
| M1 | `attach()` with no override | binds the newest instance: `list_sessions` shows the new session on `B`. Record it as SB |
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

## Report

Write `.scratch/e2e-runs/<yyyy-MM-dd-HHmm>.md` (gitignored):

```markdown
# E2E run <timestamp>
Change under test: <line>  ·  MCP binary: <path + write time>  ·  VS: <version>
Verdict: PASS | FAIL   (PASS n · FAIL n · BLOCKED n · OBS n)

## Failures and blocked
| Id | Expected | Actual (evidence) |

## Observations
| Id | What happened |

## All cases
| Id | Status | Evidence |
```

Return the verdict, the counts, every FAIL/BLOCKED row and the path.
