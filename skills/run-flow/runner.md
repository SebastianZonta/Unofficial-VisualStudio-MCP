# Flow runner

You run one flow plan against the Visual Studio debugger and judge it. Your inputs: the flow plan, the flow-testing doc, the repo root, the report path, the tactics file (debugging-tactics.md).

## Rules

- **The debugger through MCP tools only.** The shell is for the build command and a `command` trigger, nothing else. VS is the developer's to start: when it is missing, the run is BLOCKED. Read the tactics file first and follow its bind / breakpoint / evidence rules.
- **Observe only.** You evaluate the plan's read-only expressions and nothing else; `set_variable` and `set_exception_setting` stay out of a flow run, so target state stays as the code makes it. Surroundings rule still applies: resolve every anchor before setting any breakpoint.
- **Foreign breakpoints.** The first `list_breakpoints` showing many `other<N>` (pre-existing user breakpoints) costs a resume per unexpected stop. Note the count once in the report (`N foreign breakpoints present; each unexpected stop passed on without judging`) instead of justifying every one separately.
- **Rows.** The report has one row per checkpoint expectation (id `<checkpoint>.<n>`, n counting its `Read:` lines from 1) and one `outcome` row, plus one row per OBS (id `obs<n>`). Every row ends with a status.
- **Statuses.** PASS (actual equals expected), FAIL (actual differs; quote both and leave the judgement of which is right to the developer), BLOCKED (could not be checked; give the reason and the fix), OBS (something the plan does not expect, recorded, never failing). When the run stops early, every row not yet judged is BLOCKED with the reason it stopped. The verdict is PASS only when nothing is FAIL or BLOCKED.
- **Evidence.** Every status quotes the relevant fields of the tool result (or the error `code: detail`).
- **Values.** VS renders strings with quotes (`"abc"`) and decimals with their scale (`5625.00M`). Compare the value, not the decoration.
- **Budgets.** Tactics §7, except here `launch_process`, `resume`, and `run_to_cursor` use `timeoutMs` 30000 with one retry only on `vs-busy`/`timeout` (never retry a judgement); `launch_process` runs once: after a timeout the target may already be running, so treat it as a stop (step 7). The one exception is the 2000 ms `resume` that hands a debug session back to its target (step 5, several debug sessions): its `timeout` is expected, never retried, never a stop. This once-only cap governs flow runs; the debugger's wider backoff does not apply.

## Steps

1. **Read.** Read the plan and the doc. For each target the checkpoints name, note its solution and how it is bound: `launch` (exe, args, working dir) or `attach` (process name); a `launch` trigger of `defaults` means the doc's default arguments. Paths are relative to the repo root. Note the plan's trigger: `launch`, `command`, or `human` by its last line (earlier lines are `launch` lines starting targets), exactly as written. A plan with no trigger stops the run: every row is BLOCKED "plan has no trigger; add one through plan-flow", and you go to step 8. Done when every checkpoint's target resolves to a doc row and you have the trigger.
2. **Preconditions.** Check, in order: `list_instances` has a reachable entry whose solution name is each target's solution (fix: "open `<solution path>` in VS — see doc Elevation"); the doc's build command, run in the repo root, exits 0 (fix: "build fails: `<first error line>`"); each `launch` target's exe exists (fix: "no exe at `<path>`; check the doc's target"). The first one that fails stops the run: every row is BLOCKED with its fix, and you go to step 8. Done when all three hold.
3. **Resolve anchors.** For each checkpoint, grep its anchor in its file; the match inside its method's line range is its current line (the line hint only helps you find the method). Resolve every checkpoint before acting on any miss. Any anchor with no match inside its method (or a file or method no longer there) stops the run, with no breakpoint set: each stale checkpoint's rows are BLOCKED with "plan stale: checkpoint <N> anchor `<anchor>` not found in `<method>`", the rest with "not run: plan stale", and you go to step 8. Done when every checkpoint has a current line.
4. **Bind.** For each solution the targets name, `attach` with `instanceOverride` set to its entry's display name: one debug session per VS instance, each `sessionId` kept with its solution. Every later call for a target goes to its solution's session. For an `attach` target, `list_processes` with `nameFilter` set to its process name and keep the entries whose name is exactly that name (case and `.exe` aside). None stops the run: its rows are BLOCKED "no running `<process name>`; start `<target>`, then rerun"; several stop it too, BLOCKED "several `<process name>` processes (pids `<pids>`); leave one running, then rerun"; either way go to step 7. With exactly one, `attach_process` its pid with `consent: true` (the plan naming the target is the consent) and `expectedProcessName` set to its name. `set_breakpoint`, in its target's session, on each checkpoint's file (absolute path) and current line, with `condition` set to its `Stop when` when it has one; note the file:line each breakpoint bound to. Done when every target is bound and every checkpoint has a breakpoint id.
5. **Run.** Fire the trigger:
   - `launch`: `launch_process`, in its session, the trigger's target exe (absolute path) with the trigger's args and the working dir.
   - `command`: `launch_process`, each in its session, every `launch` target the checkpoints name, with the args of its `launch` line in the trigger, else its doc defaults; then start the command in bash from the repo root, character for character as the plan writes it, in the background (it may be waiting on a response a breakpoint holds).
   - `human`: start each `launch` target as for `command`, then reply with only `Waiting: <the plan's instruction, verbatim>` and wait to be resumed. `done` means the developer acted; continue from `list_breakpoints` (its `mode` and `lastHit`). `could not: <reason>` stops the run: rows not yet judged are BLOCKED "developer could not act: `<reason>`"; go to step 7.

   Then reach each break: a break the trigger already caused is judged first; after that, from `mode: break`, `resume`; from `mode: run`, `run_to_cursor` at the next unjudged checkpoint's bound file:line (it waits for the next stop, and any earlier breakpoint wins). At each break, match `lastHit` to the checkpoint whose breakpoint bound there. A checkpoint is judged at its first hit: `evaluate` each of its expressions and judge each against the plan. Later hits of a judged checkpoint are passed on, as is a break matching no checkpoint, which is OBS (quote `lastHit` and `read_stack`'s top frame). Done when the target has run to the end (`mode: design`), or, for a `command` or `human` trigger, when every checkpoint is judged and the target is passed on from its last break; then read a `command` trigger's exit code and output.
   - Several debug sessions: the next unjudged checkpoint's target picks the debug session, and arriving in one already in `mode: break` counts as a break to match first. Before turning to another debug session, match the break the one you leave is in (judge it or pass it on), then `resume` it with `timeoutMs` 2000 so its target runs on (a call into the other service may wait on it). With several `launch`ed targets, the run is at its end when each has reached `mode: design`.
   - A tool error, or a `timeout` status after the one retry, stops the run (rows BLOCKED with the `code: detail`); go to step 7.
   - A checkpoint never hit is BLOCKED "not reached" for each of its rows, quoting its target's `launch_process` call (args, `processId`) as evidence; a `run_to_cursor` that comes back `mode: run` with no hit, after the one retry, means the checkpoints not yet judged were not reached.
   - Checkpoints hit in a different order from the plan's is OBS.
6. **Expected outcome.** Judge the `outcome` row from what the tools showed: the target running to the end, and the values read at the checkpoints. For a `command` trigger, also its exit code and output. A part of the outcome no tool shows (a target's console output or exit code) is OBS "not observable under the debugger", quoting the closest evidence. Done when the `outcome` row has a status.
7. **Clean up** without ending the debugging: `remove_breakpoint` each breakpoint you set in it, then confirm with `list_breakpoints` that none of your ids remain — but no finishing `resume`, so each target stays in its current mode (a `launch` target already in `mode: design` simply exited). Then `detach` the session, even when an earlier cleanup call in it failed (`detach` frees the binding and deletes owned leftovers, never the target).

   Done when your breakpoints are gone and every session you attached is detached.
8. **Report.** Write the report to the report path (create its directory if missing):

   ```markdown
   # <flow> run <run tag from the report file name>

   Verdict: PASS | FAIL

   | # | Checkpoint | Expectation | Status | Evidence |
   |---|---|---|---|---|
   | 1.1 | <checkpoint title> | `<expression>` = `<expected>` | PASS | `evaluate`: value `<actual>` |
   | outcome | | <expected outcome> | ... | ... |

   ## Observations

   - <each OBS row, with evidence>

   ## Cleanup

   <per session: breakpoints removed with the `list_breakpoints` evidence, session detached, target left in its mode; "nothing bound" when the run stopped before step 4; "target exited" when its mode is `design`>
   ```

   Done when the file is on disk with every row.
9. **Return** this block as your whole reply. Done when it is sent.

   ```
   Verdict: PASS | FAIL
   Report: <report path>
   Stale: yes (checkpoints <N, ...>) | no
   <every FAIL, BLOCKED, and OBS row, as in the report table; "no non-PASS rows" when there are none>
   ```
