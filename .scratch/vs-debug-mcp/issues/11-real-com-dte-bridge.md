# 11: Real COM/DTE bridge

**What to build:** the one bridge implementation that swaps the fake for a live Visual Studio 2026: instance discovery, automation wiring, and call serialization behind the single bridge interface, so the already-built tools drive a real IDE untouched.

**Blocked by:** 07 (Host skeleton plus attach round-trip).

Status: resolved

- [x] Discovery enumerates the running-object table, binds newest VS 2026, honors an explicit override, and fails cleanly when absent
- [x] All automation calls serialize through one dispatcher thread; live automation objects are snapshotted to plain data on every call and stale handles re-fetched after execution control
- [x] Break-mode gating checked before inspect/evaluate against the real debugger mode
- [x] Manual check: attach plus one breakpoint set/list against a real VS 2026 with the open solution

## Comments

Implemented 2026-10-18. `DteDebugBridge : IDebugBridge` (early-bound EnvDTE v18 PIAs, build-time only; runtime binds the ROT object, never a hardcoded ProgID) + `VsDiscovery` (ROT-newest selection, pure/tested), `ComDispatcher` (single STA thread, FIFO, timeout-aware), `ComErrorMapper` (RPC_E_CALL_REJECTED→vs-busy, E_ACCESSDENIED→elevation-mismatch, else automation-failed+HR). `Program.cs`: `--live` selects the DTE bridge (default stays fake); `--live-check <file> <line>` builder harness (not a tool). Tools untouched — the seam held. Tests: 36/36 pass (25 prior + 11 discovery/mapper/dispatcher). Full server smoke-tested in both modes.

Live verification vs real VS 2026 (v18 Community, sample console solution open): `attach ok mode=design`, `set ok id=bp1 bound=True`, `list ok count=1` with file:line, `remove ok`. VS closed afterwards.

Build-time fidelity findings (research 02 §8a resolved): (1) EnvDTE exposes no pending-vs-bound flag — a successful `Add` reports bound:true; bind failures surface as never-hit breakpoints. (2) `StackFrame` exposes no file/line — real frames carry function+module, file/line null. (3) `Breakpoints.Add` returns the collection (12-param overload); the added bp is the last item. (4) Real ROT format confirmed `!VisualStudio.DTE.18.0:pid`. Timeout results carry lastHit:null (stale cache removed — honest unknown). Fidelity note: PIA references are machine-path HintPaths; if VS-path drift bites, switch to the `Microsoft.VisualStudio.Interop` NuGet package. Process attach-by-ID + consent = ticket 12 scope (interface has no PID path yet). No git repo, nothing to commit.
