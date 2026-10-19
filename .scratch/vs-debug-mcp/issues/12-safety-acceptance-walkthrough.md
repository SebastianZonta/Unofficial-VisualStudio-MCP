# 12: Safety hardening plus acceptance walkthrough

**What to build:** the proven core loop: consent-gated process attach, named environment failures, and the scripted sample-app walkthrough on real VS 2026 that closes out the spec.

**Blocked by:** 08 (Breakpoints end to end), 09 (Execution control end to end), 10 (Inspection end to end), 11 (Real COM/DTE bridge).

Status: resolved

- [x] Arbitrary process-id attach requires explicit consent plus same-user; solution launch stays frictionless
- [x] Elevation mismatch and IDE-busy surface as named errors with remediation text
- [x] Builder runs the scripted core loop against a sample C# console app on real VS 2026: attach, set breakpoint, resume, step, read stack, read variables, read-only evaluate
- [x] Bind-warning poll path and each named error path exercised at least once and recorded

## Comments

Implemented 2026-10-18. New `attach_process` tool (`SessionTools`; `processId`, `consent: false` default, `timeoutMs = 30000`): consent-first (no VS touched without it), then PID lookup (`LocalProcesses`), then same-user ownership check (WMI `Win32_Process.GetOwner` vs `WindowsIdentity`, fail closed on unknown), then `Process.Attach()`. `attach` unchanged — solution launch stays frictionless. Bridge interface +1 method; fake models owners via `ProcessOwners`/`CurrentUser`; `ProcessOwner` helper unit-tested live (own PID resolves to current user). `LiveCheck --live-check-full` drives the scripted loop.

Live acceptance vs real VS 2026 (sample console app, prebuilt Debug, solution open):
attach ok mode=design → set ok bp1 bound=True → resume ok mode=break → stack ok (1 frame `Program.<Main>$`) → variables ok (`args: string[]`) → evaluate ok (`args.Length = 0 (int)`) → step ok mode=design → remove ok. VS closed afterwards.

Fidelity note: live `lastHit` came back empty after the break (`BreakpointLastHit` unreadable at that instant) — mode=break still proves the landing; fake covers lastHit echo. Tests: 44/44 pass.

Error-path matrix (each exercised ≥ once): no-session, stale-session (unit 07); invalid-argument (08/09/10); breakpoint-not-found (08); frame/path-not-found (10); not-in-break-mode (09/10); not-running/already-running (09); timeout-as-data (09 fake); timeout named (10 fake); invalid-expression (10 fake); consent-required/process-not-found/user-mismatch (12 fake + WMI self-test); vs-busy/elevation-mismatch/automation-failed (11 mapper tests; not triggered live — VS was responsive and same-elevation); bind-warning poll (08 fake; not forced live — would need unloaded symbols). No git repo at implementation time; repo creation is the separate last step.
