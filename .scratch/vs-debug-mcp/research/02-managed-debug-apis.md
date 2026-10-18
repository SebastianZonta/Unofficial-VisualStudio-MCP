# 02 — Managed debug API surface for the minimal core (VS 2026, C#)

Scope: same-machine, C#-only v1. Minimal core: set/clear/list breakpoints (+bind states),
break/continue, step in/out/over, threads/call stacks, scopes/locals/args, expression evaluate.

## Recommendation up front

For the v1 bridge, **automate the IDE via EnvDTE (Debugger2/Debugger3/Debugger5 +
Breakpoint/Breakpoint2/Expression/StackFrame/Thread), not Concord (Dkm*) and not the
legacy SDM interfaces (IDebug*)**. EnvDTE is the only automation-facing API: it is stable
COM automation, callable out-of-proc from a same-machine bridge, and covers the entire
minimal core in one object model. Concord's Dkm* API is for *implementing* debug-engine
components inside the VS process (in-proc package with async entry points, thread
affinity, session lifetime management) — far more machinery than an automation bridge
needs. The legacy SDM interfaces (IDebugBreakpoint2, IDebugStackFrame2,
IDebugExpressionContext2, …) are engine-implementor interfaces, not something a bridge
calls directly; the managed (.NET) debug engine that ships with VS already implements
them for C#.

## Per-capability mapping (EnvDTE — the automation API)

All entry: `DTE2.Debugger` (cast to `EnvDTE80.Debugger2`; then to `EnvDTE90.Debugger3`
/ `EnvDTE100.Debugger5` for newer members). One debugger object per VS instance.

| Core need | Concrete API | Notes |
|---|---|---|
| Set file breakpoint | `Breakpoints.Add(File, Line, Column)` | Returns `Breakpoint`. Line is 1-based. `Column` usually 1. |
| Set function breakpoint | `Breakpoints.Add(Function, …)` with `Language="C#"` | Fallback when no source line context. |
| List breakpoints | `Debugger2.Breakpoints` (`Breakpoints` collection) + `Breakpoint.{File,FileLine,FileColumn,FunctionName,Enabled,…}` | |
| Delete / enable-disable | `Breakpoint.Delete()`, `Breakpoint.Enabled = true/false` | v1 core; conditions/hit-counts are out of scope but the properties exist (`Condition`, `HitCountType`, …). |
| Bind state | `EnvDTE80.Breakpoint2` adds what v1 needs to poll/report | `Breakpoint2` exposes pending-vs-bound-relevant members used to report "unbound/warning" state (the Breakpoints window shows the same data). Exact bind enumeration at the address level lives in Concord (`DkmPendingBreakpoint` → `DkmBoundBreakpoint`); automation only needs pending/bound/enabled + warning surfaced via Breakpoint2. |
| Break (pause) | `Debugger.Break(bool WaitForBreakMode)` / `Process.Break(bool)` | `WaitForBreakMode=true` blocks until break mode — bridge should prefer it and add its own timeout. |
| Continue | `Debugger.Go(bool WaitForBreakOrEnd)` | `true` waits synchronously — use with timeout/cancellation on the bridge side. |
| Step in / out / over | `Debugger.StepInto / StepOut / StepOver(bool WaitForBreakOrEnd)` | Same wait-flag pattern. No "step to cursor" needed for v1 (`RunToCursor` exists but excluded). |
| Threads | `Debugger.CurrentThread`, `Thread.{ID,Name,IsAlive,IsFrozen,StackFrames}`, `Threads` via program | `Thread.Freeze()/Thaw()` exist — v1 must NOT expose them (safety). Multi-thread listing beyond current thread is best-effort. |
| Call stack | `Thread.StackFrames` (`StackFrames` collection) → `StackFrame.{FunctionName,Module,FileName?,LineNumber?}`; `Debugger.CurrentStackFrame` get/set | Frame selection = set `CurrentStackFrame`, then evaluate. |
| Scopes / locals / args | `StackFrame.Locals`, `StackFrame.Arguments` (`Expressions` collections) → `Expression.{Name,Value,Type,DataMembers,IsValidValue}` | Drill-down via `Expression.DataMembers`. This is the v1 "variables" path — no need for Autos/Locals-window internals. |
| Expression evaluate | `Debugger.GetExpression(Text, UseAutoExpand=true, Timeout)` → `Expression`; `EnvDTE100.Expression2` extends it; `Debugger.ExecuteStatement(stmt, timeout, TreatAsExpression)` for statement-with-side-effects | `IsValidValue=false` means parsed-but-not-evaluable. `Value` setter assigns (side effect — gate it). |
| Mode / state for gating | `Debugger.CurrentMode` (`dbgDesignMode/dbgBreakMode/dbgRunMode`), `Debugger.LastBreakReason`, `Debugger.BreakpointLastHit`, `Debugger.AllBreakpointsLastHit` | Bridge must poll/gate: only evaluate/list-frames in `dbgBreakMode`. |
| Events (optional) | `DebuggerEvents` (`OnEnterBreakMode`, `OnEnterRunMode`, `OnExceptionThrown`, `OnContextChanged`) via `DTE.Events.DebuggerEvents` | Polling `CurrentMode` is simpler and sufficient for v1; events are the later optimization. |
| Processes (attach/list) | `Debugger.LocalProcesses`, `Debugger.DebuggedProcesses`, `Debugger.CurrentProcess`, `Process.Attach()` / `Detach(bool)` / `Terminate(bool)` | v1 attach model: enumerate local, attach by ID. Remote transports (`Debugger2.Transports`, `GetProcesses`) excluded. |
| Stop | `Debugger.Stop(bool WaitForDesignMode)`, `TerminateAll()`, `DetachAll()` | |

Key detail — `GetExpression` signature: `GetExpression(string ExpressionText,
bool UseAutoExpand = true, int Timeout = -1)`. Timeout is in milliseconds; bridge must
always pass an explicit finite timeout (e.g. 5 s) instead of the default.

Key detail — the `WaitForBreakOrEnd` / `WaitForBreakMode` boolean on Break/Go/Step:
`true` = synchronous wait. Bridge wraps with timeout + cancellation; never expose
indefinite wait to the agent.

## What NOT to use (and why)

- **Concord Dkm* API** (`Microsoft.VisualStudio.Debugger.{Breakpoints,CallStack,Evaluation,…}`:
  `DkmPendingBreakpoint`/`DkmBoundBreakpoint`, `DkmStackWalkFrame`, `DkmInspectionContext`,
  `DkmLanguageExpression`/`DkmEvaluationResult`, `DkmVisualizedExpression`, …). This is the
  modern in-proc debug-engine extensibility model (VS 2012+, "Concord"). Consumers are
  debugger *components* running inside VS (engine/EE/visualizer add-ins, async COM entry
  points, DkmDataContainer lifetime rules). A same-machine MCP bridge does not implement a
  debug engine — it drives the existing managed engine. Using Dkm* would mean writing an
  in-proc VS package/extension anyway, then still needing IPC to the MCP server: all of
  the extension cost with none of the automation simplicity. Reference only.
- **Legacy SDM interfaces** (`Microsoft.VisualStudio.Debugger.Interop`: `IDebugBreakpoint2`,
  `IDebugPendingBreakpoint2`/`IDebugBoundBreakpoint2`, `IDebugThread2::EnumFrameInfo`,
  `IDebugStackFrame2::GetExpressionContext`, `IDebugExpressionContext2::ParseText`,
  `IDebugExpression2::EvaluateSync/Async`, `IDebugProperty2::GetPropertyInfo`). These are
  what the shipping C# engine *implements*; the bridge never calls them. Documented here
  only so the spec can say "the managed engine handles bind/evaluate; we observe results
  through EnvDTE."
- **VisualStudio.Extensibility (out-of-proc, modern) debugger surface**: as of VS 2026 it
  covers commands, editor, tool windows — there is no documented debugger-break/step/
  evaluate automation surface in it comparable to EnvDTE. Do not plan on it for v1.
- **ICorDebug**: lower-level than needed (the CLR debugging API the managed engine itself
  uses). Only relevant if the project ever bypasses VS entirely — explicitly out of scope.

## VS 2026 deltas (what changed / what to verify at build time)

1. **No EnvDTE debugger breakage expected.** VS 2026 ships an API-version compatibility
   model: VSIX extensions built for VS 2022 (API 17.x) load in 2026 without rebuild; the
   platform preserves stable-API backward compatibility. EnvDTE automation is stable COM —
   latest documented package `Microsoft.VisualStudio.Interop v17.14.x` — no Debugger6/new
   automation interface announced. Treat EnvDTE surface as unchanged in 2026.
2. **Out-of-proc COM automation still the path; verify ROT + 64-bit.** VS is 64-bit since
   2022; the bridge (also 64-bit) uses `GetActiveObject` on the DTE moniker /
   `VisualStudio.DTE.18.0` ROT entry for "attach to running instance" plus
   `DTE2.Debugger`. Instance discovery details (ROT naming, multi-instance) belong to the
   session/attach ticket, but no 2026 change invalidates the approach.
3. **Verify at build time (2026 SDK):** (a) `Breakpoint2` bind-state reporting for C# on
   .NET (Core) vs .NET Framework targets — unbound-reason fidelity differs by project
   system/symbol load; (b) `GetExpression` behavior for newer C# syntax (bridge pins to
   what the shipped C# EE accepts; version skew = "parse ok, evaluate invalid"); (c) any
   `VisualStudio.Extensibility` debugger additions in the 18.x SDK that would obsolete
   hand-rolled COM plumbing — re-check once per 18.x minor.
4. **Marketplace/packaging:** a same-machine bridge that talks to a *running* VS via COM
   needs no VSIX at all (plain local process + MCP stdio/HTTP). A VSIX is only needed if
   the design later moves logic in-proc. Prefer no-VSIX for v1 install friction.

## Reliability gaps the bridge must wrap

1. **Bind states are eventual, not transactional.** Setting a file breakpoint before
   symbols/module load yields pending-unbound; `Breakpoint2` surfaces it, but timing
   depends on build/symbol state. Bridge rule: return breakpoint id immediately with
   `bound: false, warning` rather than blocking; agent re-lists/polls.
2. **Synchronous-wait calls can hang the caller.** `Go/Step*/Break/Stop` with wait=true
   block until the debuggee transitions. Bridge must wrap every one with finite timeout +
   cancellation and report `timeout` as a first-class status (never hang the MCP call).
3. **Evaluate has side effects and no sandbox.** `GetExpression` can invoke user code
   (property getters, `ToString`); `ExecuteStatement` / `Expression.Value=` mutate state.
   Bridge rules: default evaluate is read-intent with explicit timeout; statement-execution
   and value-assignment are separate, clearly-named tools with confirmation/safety wording
   (exact policy = safety ticket's job).
4. **Stale handles.** `Thread`/`StackFrame`/`Expression` objects go stale on resume/step.
   Bridge rule: snapshot to plain data on every call; never hand out live COM pointers;
   re-fetch after any execution-control call; frame/thread IDs are session-scoped tokens.
5. **Multi-process/multi-program ambiguity.** `CurrentProcess`/`CurrentProgram` select one
   of potentially many. Bridge rule (v1): single active session; process attach binds the
   session; cross-process ops are out of scope — return an error naming the limitation.
6. **Evaluate-while-running fails.** `GetExpression` requires break mode. Bridge rule:
   check `CurrentMode == dbgBreakMode` first; return a `not-in-break-mode` error instead
   of a COM exception.
7. **DTE threading/apartment.** EnvDTE COM calls must come from a properly-initialized
   (STA/message-pumping or correctly marshalled) thread. Bridge rule: single-threaded
   dispatcher for all DTE calls; serialize automation calls; never call DTE from parallel
   request handlers. (Implementation ticket owns this.)
8. **Func-eval deadlock risk.** Evaluating expressions that require CLR func-eval while
   the debuggee holds locks can time out or deadlock; always use explicit timeout and
   surface `evaluation-timed-out` distinctly from `invalid-expression`.

## What the same-machine bridge wraps (checklist for the spec)

- Transport: local stdio/HTTP MCP server (no VSIX for v1) → COM EnvDTE against the
  discovered running VS 2026 instance (`DTE2`).
- One dispatcher thread serializing all `Debugger2` calls; per-call timeouts.
- Tool set maps 1:1 to the table above (breakpoints, break/continue, step×3, threads,
  frames, locals/args, evaluate) with plain-data DTOs: breakpoint `{id, file, line,
  enabled, bound, warning?}`, frame `{id, function, module, file?, line?}`,
  value `{name, type, value, expandable}`.
- State gating on `CurrentMode`; session binds to one process; stale-handle snapshots.
- Safety split: read-evaluate vs statement-execute vs value-assign as separate tools.

## Sources

- EnvDTE `Debugger` interface (methods/properties: Break/Go/StepInto/StepOut/StepOver/
  GetExpression/ExecuteStatement/CurrentMode/CurrentThread/CurrentStackFrame/…):
  https://learn.microsoft.com/en-us/dotnet/api/envdte.debugger?view=visualstudiosdk-2022
- `Debugger2` (supersedes Debugger; adds Transports/GetProcesses/GetExpression2/
  WriteMinidump): https://learn.microsoft.com/en-us/dotnet/api/envdte80.debugger2?view=visualstudiosdk-2022
- `Breakpoint` (File/FileLine/Enabled/Condition/HitCount/Delete):
  https://learn.microsoft.com/en-us/dotnet/api/envdte.breakpoint?view=visualstudiosdk-2022
- `Expression` (Name/Value/Type/DataMembers/IsValidValue):
  https://learn.microsoft.com/en-us/dotnet/api/envdte.expression?view=visualstudiosdk-2022
- `Thread` (ID/Name/IsAlive/StackFrames/Freeze/Thaw) via EnvDTE reference (same API set).
- `Debugger5.ExceptionGroups` (why ExceptionSettings ≠ v1 core):
  https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2012/ee886481(v=vs.110)
- Concord breakpoints model (pending→bound; why bridge doesn't use it):
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.debugger.breakpoints?view=visualstudiosdk-2022
- Concord evaluation entry points (for reference only):
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.debugger.evaluation?view=visualstudiosdk-2022
- Legacy SDM model — expression evaluation via IDebugExpressionContext2/IDebugExpression2/
  IDebugProperty2 (engine-implementor side):
  https://learn.microsoft.com/en-us/visualstudio/extensibility/debugger/evaluating-expressions?view=visualstudio
- Call-stack evaluation via IDebugThread2::EnumFrameInfo (engine-implementor side):
  https://learn.microsoft.com/en-us/visualstudio/extensibility/debugger/call-stack-evaluation?view=visualstudio
- VS 2026 extension compatibility (API-version model; 2022 extensions load unrebuilt):
  https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/docs/extensibility/migration/extension-compatibility.md
- VS 2026 release notes (18.x; no EnvDTE debugger-surface change announced):
  https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes?view=visualstudio
