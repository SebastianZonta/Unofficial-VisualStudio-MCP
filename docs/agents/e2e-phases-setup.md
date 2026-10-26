# E2E phases P0-P4 + Sandbox reference

Part of the E2E pass. Start at [e2e.md](e2e.md), then read this file only if it owns your slice.

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
| S5 | Copy `Sandbox/` (without `bin/`, `obj/`, `.vs/`) to `%TEMP%\SandboxB` and build it there | build green; distinct file paths for VS B |

If S2/S3 fail, mark every later case BLOCKED and go to P11.

## P1: discovery (session-less)

| Id | Step | Expected |
|---|---|---|
| D1 | `list_instances` | contains `A` (reachable, solution path ends `Sandbox.slnx`). Other instances may appear: ignore them |
| D2 | `list_sessions` | no session bound to `A` |
| D3 | `list_processes(nameFilter: "sandbox")` | exactly the `waitPid` entry (case-insensitive filter), `mine: true` |
| D4 | `list_processes(ownedOnly: false)` | at least as many entries as `list_processes()`; any `mine: false` entry has an owner or error text |
| X1 | `set_breakpoint(file: "", line: 0)` | `invalid-argument` with the code in the machine-readable channel (not just the message prefix). Manual: open a modal dialog in VS, then any call → `vs-busy`, never a crash |

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
| C10 | `threads` scenario: logpoint at `WORKER` `"{id}/{n}"`; `launch_process(Sandbox.exe, "threads")` | never stops; ends `design`; `Log` gains exactly 20 new entries for this logpoint's id, each pair `1/1` … `4/5` exactly once (the session `Log` is cumulative across launches — C6's entries persist). Canary for issue `10-logpoint-hit-surfaces-as-stop`: a stop here reopens it for a PollResume-race investigation; a clean pass closes it (set its `Status:` to `resolved`) |
