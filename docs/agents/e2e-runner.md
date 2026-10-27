# E2E runner: roles, parallel run, rules

Part of the E2E pass. Start at [e2e.md](e2e.md), then read this file only if it owns your slice.

## Roles

**Main agent (orchestrator)** does preflight, fans out, merges, then triages:

1. `dotnet build VsDebugMcp.slnx` and `dotnet test VsDebugMcp.slnx --no-build` green.
2. Publish the MCP and get the host restarted on the fresh binary (live is the
   default; `--live` is accepted as a no-op)
   (stop the clients first: the exe is locked while hosts run). Restarting the
   host is the user's action; ask, don't work around it.
3. Check the host: the vs-debug toolset must list all 21 tools (`run_to_cursor`,
   `list_exception_settings`, `set_exception_setting` included). If any tool is missing, the host runs a stale binary: stop.
4. Pick a run tag `<yyyy-MM-dd-HHmm>`, copy `Sandbox/` (without `bin/`, `obj/`)
   to two temp dirs (one for R2, one for R3; R1 uses the repo source), then
   `Unblock-File -Recurse` both copies (they carry Mark-of-the-Web or VS sits
   at `solution unreadable`), then fan
   out exactly three subagents (R1, R2, R3) on **muse-spark-1.3-contributor** (opencode) / **sonnet** (Claude Code),
   one prompt template instantiated per slice (below).
5. Merge the three partials (`.scratch/e2e-runs/<tag>-R1.md` … `-R3.md`) into
   the single verdict file (`.scratch/e2e-runs/<tag>.md`, same format as
   [Report](e2e.md#report)): merged counts plus the combined failures/blocked and
   observations tables. Verdict is PASS only if every non-OBS case passes in
   all three slices. Then run phase **H** (human-assisted) yourself with the
   user, appended to the merged file.
6. Read the merged report. Triage every FAIL and BLOCKED into `.scratch/` issues, and
   relay the verdict to the user. A FAIL is never "fixed" by editing this doc
   to match the observed behaviour without saying so.

```text
Agent(
  description: "E2E slice against Sandbox",
  subagent_type: "general-purpose",
  model: "opencode-go/muse-spark-1.3-contributor",  # Claude Code: model: "sonnet"
  prompt: """
    You are E2E runner <R1|R2|R3> for the VS debug MCP at <repo root>.
    Read docs/agents/e2e.md, docs/agents/e2e-runner.md and only your phase file(s), then run your slice in order, exactly as
    written: R1 = P0 + P1-P4 + P9, R2 = P0 + P5-P7 + P10, R3 = P0 + P8 + P11
    (plus the Parallel-run rules for your temp copy, PID scoping and cleanup).
    Run tag: <tag>. Your Sandbox copy: <repo Sandbox/ for R1 | temp dir for R2/R3>.
    Your VS display-name prefix, session ids and target PIDs are yours alone.
    Load the vs-debug MCP tools via ToolSearch (query "+vs-debug", max_results 20).
    Never touch a VS instance you did not start. Never skip a case silently.
    Write your partial report to .scratch/e2e-runs/<tag>-R<N>.md in the doc's
    format and return: verdict, PASS/FAIL/BLOCKED/OBS
    counts, every FAIL and BLOCKED with evidence, and the report path.
    Change under test: <one line>. Extra focus: <optional>.
  """)
```

## Parallel run (3 runners, sliced phases)

One template, three instantiations differing only in slice (phase range), run
tag and Sandbox copy. Staggering the three P0 starts is not required (distinct
temp-copy dirs, no shared bin lock); VS startup polling keeps its 10 s / 3 min
budget.

- **Per-runner P0.** Each runner builds, resolves marker lines dynamically
  (never hardcodes), starts its own VS, records its own wait PID. S5's
  `%TEMP%\SandboxB` is the single-runner name: in a parallel run every temp
  path is runner-scoped (run tag + R-suffix), including R2's second copy for
  P10's M0. The repo keeps exactly one Sandbox source; scenario edits stay a
  one-file change plus the marker table, and copies can never drift.
  P9 runs in R1 against R1's own wait PID — its only coupling is the P0 that
  minted it, so it travels with its runner where P8's live-target chain cannot.
- **File paths.** R1 uses the repo `Sandbox/`; R2/R3 use the main agent's temp
  copies. Pass breakpoint `file` args from your own copy only, so `file:line`
  identity, `lastHit` assertions and the two-VS path-distinction check (M6)
  stay attributable, and a leaked breakpoint in one runner cannot flake
  another. Each runner deletes every temp copy it used in cleanup (Z3); per-run
  machinery never lands on disk.
- **PID scoping (surgical).** Every other case, table and budget is
  byte-identical to the single-runner pass; only three assertions scope to the
  runner's own recorded PIDs: D3 (exactly your own `waitPid` entry, not the
  only Sandbox process on the box), E14 (no process starts: nothing new named
  Sandbox outside your own PIDs), Z3 (stop only the Sandbox processes and VS
  instances you started, and only those; your `sandbox` filter reads empty).
- **Unchanged strictness.** Retry once on `vs-busy` or a `timeout` before
  calling it a FAIL; never retry an assertion. `launch_process`/`resume`
  timeout budgets are unchanged. The exception-settings restore chain (E25)
  keeps its must-run-cleanup semantics inside R3. Phase H is unchanged.
- **Watch list (pilot only).** CPU contention from three concurrent VS
  startups, the P10 two-VS spike (four VS instances plus the maintainer's
  own), and any leaked-breakpoint or PID-reuse surprise the scoping above is
  designed to contain. If VS-startup contention dominates, the follow-up is
  staggering P0 starts, never re-merging slices.

### Slice map (tool-slices 02–05)

Which tool slice owns which phases, runner, fake suite and live audit case.
Slices A/B (platform) and G (contracts) own no phases: their gates are the
fake-only `ComDispatcherTests`, `ComPlumbingTests`, `VsDiscoveryTests` and
`DocsContractTests` suites plus the manual `BusyVs` check below.

| Slice | Phases | Runner | Fake suite | Live audit (`AuditE2ETests`) |
|---|---|---|---|---|
| C session/process/launch | P1, P2, P9, P10 (+ launch half of P8) | R1 (P1, P2, P9), R2 (P10), R3 (P8) | `AttachTests`, `AttachProcessTests`, `LaunchProcessTests`, `ListInstancesTests`, `ListProcessesTests`, `MultiSessionTests` | `ParallelAttach_SameInstance_SameSession`, `AttachMode_ReportsKnownMode`, `OneTargetRule_BothDirections` |
| D breakpoints/modules | P3, P4 | R1 | `BreakpointTests`, `ConditionalBreakpointTests`, `OccupiedLocationTests`, `ListModulesTests` | `StableIds_BreakpointIdentity` (conditions/logpoints/symbol-honesty live in the manual P4 cases) |
| E execution/exceptions | P7, P8 | R2 (P7), R3 (P8) | `ExecutionTests`, `RunToCursorTests`, `ExceptionConfigTests` | `MediumAudit_BridgeParity` (step lastHit shape, recycled-PID guard, module states, frameless frames) plus manual P8/E13/E21–E25 cases |
| F inspection/write | P5, P6 | R2 | `InspectionTests`, `ReadOnlyGateTests`, `SetVariableTests`, `TruncationContractTests` | `ReadOnlyGate_NoSideEffects`, `FrameTrue_StackLevels`, `EvaluateBudget_FullTimeoutMsHonored` |

Skipped live cases (explicit reason, not coverage): `BusyVs_StillSurfacesAfterRetryWindow`
(manual: needs a human-held modal dialog in VS). Devenv-guard and symbol-state
honesty are pinned on the fake (`Attach_refuses_devenv_pid`,
`Launching_devenv_is_refused…`, `ListModulesTests`) and exercised by the manual
P9/P3 cases; they have no automated audit case.

## Runner rules

- **Case ids.** Report every case by its id. Statuses: PASS, FAIL (actual
  differs from expected), BLOCKED (couldn't run; give the reason), OBS
  (observation: record what happened; no expected value).
- **Evidence.** For each case, quote the relevant fields of the tool result (or
  the error `code: detail`).
- **Retries.** Retry once on `vs-busy` or a `timeout` status before calling it a
  FAIL. Never retry an assertion until it passes. After `vs-busy` (or `timeout`)
  on a non-idempotent tool (`step`; `resume`/`run_to_cursor` already re-snapshot
  by design), re-snapshot state first (`read_stack` / mode+lastHit) and retry
  only if still in the pre-call state.
- **Isolation.** Every `attach` passes `instanceOverride` (the display name of an
  instance you started). The only exception is M1, which targets the newest
  instance on purpose. Never touch a VS instance, debug session or target
  process another runner started; stop only the PIDs you started. Act on VS only through MCP tools. The shell is for building, starting
  and stopping the processes this pass owns, nothing else.
- **Budgets.** Use `timeoutMs` 30000 for `launch_process`/`resume` unless the
  case says otherwise. "Run to end" means `resume` until `mode: design`.
- **Values.** VS renders strings with quotes (`"Montevideo"`) and decimals
  with their scale (`5625.00`, or `5625.00M`). Compare the value, not the
  decoration.
- **Bound target.** One target process per debug session. Before each
  `launch_process`, the previous target must have exited (`mode: design`).
