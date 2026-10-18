# 01 — VS integration paths for same-machine C# debugging (VS 2026)

Question: which path lets a same-machine MCP server drive VS 2026 C# debugging —
(A) tiny VS extension exposing a local bridge (stdio/HTTP/named pipe),
(B) out-of-proc COM/DTE automation with no extension,
(C) Concord/MIEngine debug-engine APIs?

## Options compared

### A. VS extension + local bridge (in-proc VSIX, brokered service / named pipe / localhost HTTP)

- How it works: a VSIX runs inside (or alongside, brokered-service) devenv.exe, uses in-proc debugger
  services (IVsDebugger, EnvDTE, Concord SDM APIs), and exposes a localhost endpoint the MCP
  server (or the extension itself hosting MCP over stdio) talks to.
- Install friction: HIGH relative to B. Requires VSIX build, VS SDK targeting, install into
  production VS (vsixinstaller, restart), per-major-version retargeting, signing/trust prompts.
  Debugging the extension itself needs the Experimental Instance (F5) workflow.
- Instance discovery/lifetime: trivially solved — the bridge lives *inside* the target VS instance,
  so no ROT lookup or multi-instance ambiguity.
- Failure modes: VSIX load failures, version mismatch, extension crash/hang policy (better under
  the new out-of-proc model), localhost port/firewall prompts, auth on the local socket.
- Sources:
  - https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/visualstudio-extensibility?view=visualstudio
  - https://learn.microsoft.com/en-us/visualstudio/extensibility/launch-visual-studio-dte?view=visualstudio

### B. Out-of-proc COM / EnvDTE automation (no extension)

- How it works: a standalone .NET exe on the same machine binds to the running VS instance via the
  Running Object Table (ROT) / `GetObject("VisualStudio.DTE...")`, then drives
  `EnvDTE.Debugger` (+ `Debugger2`–`Debugger5`): `Breakpoints.Add`, `Go`, `Break`, `Stop`,
  `StepInto/StepOver/StepOut`, `CurrentStackFrame` → `Locals`/`Arguments`,
  `GetExpression`/`ExecuteStatement`, `CurrentMode`, `LastBreakReason`, `DebuggedProcesses`/`LocalProcesses`.
- Covers the whole minimal core (break, step in/out/over, stacks, locals/args, evaluate,
  breakpoint bind states via `Breakpoint2`). Conditional breakpoints exist on `Breakpoint2`
  (`ConditionType`/`ConditionExpression`); one known gap is programmatic *dependent/grouped*
  breakpoints (open Developer Community idea, non-blocking for v1).
- Install friction: LOWEST. One .NET 8 exe, no VSIX, no VS restart, works against the user's
  already-open VS. Proven precedent below.
- Instance discovery/lifetime: the weak point. ROT enumeration needed for multiple instances;
  ProgID version suffix changes per major release; must handle "VS not running / no solution".
- Failure modes: COM STA/UI-thread blocking (a call can hang while VS is busy or a modal dialog
  is open); cross-elevation COM fails silently (VS and bridge must run at same elevation);
  no push events without a `DebuggerEvents` advise sink, so break-state detection typically polls;
  `GetExpression` fidelity is below the full Concord expression evaluator.
- Sources:
  - EnvDTE.Debugger reference (properties + methods): https://learn.microsoft.com/en-us/dotnet/api/envdte.debugger?view=visualstudiosdk-2022
  - Breakpoints.Add: https://learn.microsoft.com/en-us/dotnet/api/envdte.breakpoints.add?view=visualstudiosdk-2022
  - ROT enumeration pattern: https://www.meziantou.net/control-visual-studio-from-an-external-application.htm
  - DTE ProgID/side-by-side: https://learn.microsoft.com/en-us/visualstudio/extensibility/launch-visual-studio-dte?view=visualstudio
  - Dependent-breakpoint API gap: https://developercommunity.visualstudio.com/t/EnvDTE-Breakpoint2-lacks-API-to-create-d/11117102
- Precedent — **VsBridge** (`IsaacSimms/VsDebugClaudeMcpAndSkill`): .NET 8 `VsBridge.exe`,
  `Claude Code ─(MCP/stdio)─> VsBridge.exe ─(COM/EnvDTE)─> Visual Studio`, zero-extension,
  claims VS 2022 **and 2026** support, ~11 tools (`vs_status`, `vs_launch`, `vs_stop`,
  `vs_set_breakpoint`, `vs_remove_breakpoint`, `vs_get_locals`, `vs_evaluate`,
  `vs_step_over/into/out`, `vs_get_callstack`), 33 Moq tests over mocked EnvDTE COM interfaces.
  Documents exactly the failure modes above (elevation, newest-instance selection, COM hangs).
  - https://github.com/IsaacSimms/VsDebugClaudeMcpAndSkill

### C. Concord / MIEngine custom debug engine

- What it is: Concord is VS's debug engine since VS 2012 (collection of `IDebug*` components);
  `Microsoft.VisualStudio.Debugger.Engine` NuGet (e.g. 17.14.x) + samples repo let you author
  engines, program nodes, expression evaluators. MIEngine is the open-source MI (gdb/lldb) engine.
- Verdict: WRONG LAYER for this ticket. Writing a custom engine means implementing/registering a
  debug engine — weeks of work — to replace the managed engine we want to *drive*, not replace.
  MIEngine targets MI debuggers (gdb/lldb), not managed C#. None of the minimal core (bind to the
  existing C# session, F5/attach, step, evaluate) gets easier via Concord; DTE already sits on top
  of it.
- Sources:
  - https://github.com/microsoft/ConcordExtensibilitySamples
  - https://www.nuget.org/packages/Microsoft.VisualStudio.Debugger.Engine/
  - https://github.com/microsoft/MIEngine
  - https://learn.microsoft.com/en-us/visualstudio/extensibility/debugger/getting-started-with-debugger-extensibility?view=visualstudio

## VS 2026 deltas (what changed vs 2022 for this decision)

- Two extension models coexist: classic in-proc VSSDK still works; the newer
  `VisualStudio.Extensibility` model (out-of-proc, async, brokered services, hot-load) is the
  stated direction. Its debugger surface today is narrow (debugger *visualizers* are the documented
  sample area) — there is **no general-purpose launch/step/breakpoint automation API** in the new
  model. So the new model does not remove the need for EnvDTE/in-proc services for debugger control.
- Microsoft's stated platform goal is to minimize/eliminate breaking changes to stable APIs across
  the 2022→2026 migration ("effortless migration"); EnvDTE/COM automation is the stable legacy
  surface and VsBridge claims 2026 works unchanged. Concrete delta to verify at build time: the
  DTE ProgID/ROT moniker version suffix for the 2026 major release — enumerate ROT rather than
  hardcoding it.
- Adjacent context: Visual Studio itself is now an MCP *client* (connect local/remote MCP servers
  via `mcp.json`, GA Aug 2025). That validates MCP-over-stdio shape but is the opposite direction
  (VS consuming tools, not exposing the debugger).
- Sources:
  - https://devblogs.microsoft.com/visualstudio/modernizing-visual-studio-extension-compatibility-effortless-migration-for-extension-developers-and-users/
  - https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes
  - https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers?view=visualstudio
  - https://devblogs.microsoft.com/visualstudio/mcp-is-now-generally-available-in-visual-studio/

## Recommendation

- **Recommended path for the spec: B — out-of-proc COM/DTE automation, no extension.**
  Reasons: (1) covers the full minimal core with a stable, documented API; (2) lowest install and
  dev friction — one local exe, no VSIX packaging/signing/restart/per-version retarget; (3) live
  precedent (VsBridge) proves the topology including a VS 2026 claim, with a tool list (~11 tools)
  already inside our ~10-tool budget; (4) VS 2026 changes do not obsolete it, while the new
  extension model adds no debugger-automation capability that would justify an extension.
- **Fallback: A — tiny in-proc VSIX bridge** (named pipe or localhost HTTP w/ token), if build-time
  validation hits any of: unresolvable multi-instance targeting, unacceptable COM-call hangs
  (modal dialogs), missing push events, or elevation issues in the target environment. The VSIX
  would expose only what DTE cannot reliably do (instance-scoped session id, break-event push,
  health ping) — not a second debugger implementation.
- **Not recommended: C (Concord/MIEngine)** — engine authorship is an order of magnitude more work
  for zero gain on the stated minimal core.

## Open risks / validation items for the spec + builder

1. DTE ProgID/ROT moniker for VS 2026 (don't hardcode; enumerate ROT, prefer newest, allow
   instance override). Verify against a real 2026 install.
2. Break-event latency: polling `CurrentMode`/`BreakpointLastHit` vs `DebuggerEvents` sink —
   measure and set spec timeouts accordingly.
3. COM STA hangs: define a call timeout + "VS busy" error shape; document modal-dialog and
   elevation (same-integrity-level) requirements.
4. `GetExpression`/`ExecuteStatement` fidelity and side-effect policy for evaluate (spec guardrail).
5. Attach model: recommend launch (F5)/resume for v1; attaching to arbitrary processes needs
   consent + transport/engine selection (`Debugger2/Debugger3`) — defer or gate explicitly.
6. Single active debug session assumption; behavior with multi-target / multi-process debugging.
