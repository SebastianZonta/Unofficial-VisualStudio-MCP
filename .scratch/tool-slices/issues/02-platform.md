# 02 — Platform slices A+B: infra/build-publish and COM core

Status: ready-for-human

## Task

- Slice A owns build/publish only: VS discovery chain with clear no-VS message, analyzer asymmetry preserved, pinned SDK handling, host flag dispatch order, publish-to-fresh-empty-dir plus repoint-client rule, exe-locked retry note.
- Slice B owns COM core only: single STA dispatcher cap and queue behavior, message-filter window and shared-root fix, COM-to-domain error map (`vs-busy` vs `vs-not-running` vs `elevation-mismatch`), ROT enumerate-then-get race, DTE version prefix.
- No tool slice edits these paths while A/B are in flight.

## Done when

- Clean `dotnet build` rule documented for missing-VS case; publish checklist blocks stale `System.Management.dll`.
- Dispatcher and filter behavior pinned by unit tests; live `BusyVs` path verified manually once.

## Notes

Highest seam stays the dispatcher boundary; no new seam.

## Comments

- Agent-done: `VsDebugMcp.csproj` gains a `CheckVsInstall` target that fails
  fast with `No Visual Studio 2026 EnvDTE found` (verified by building with a
  bogus `VsPublicAssemblies` path) instead of confusing unknown-type errors;
  `TROUBLESHOOTING.md` documents the no-VS case. `INSTALL.md` now mandates a
  fresh empty publish folder, the ~314 KB `System.Management.dll` check,
  repoint-and-restart, and the exe-locked note.
- Agent-done: `ComPlumbingTests.Disconnected_hresults_count_as_dead_vs_not_busy`
  pins `IsDisconnected` (four dead-VS HRESULTs true; busy/denied false),
  completing the vs-busy vs vs-not-running vs elevation-mismatch map at the
  mapper seam. Dispatcher cap/queue/STA/filter-rooted plus `VsDiscovery`
  prefix/ordering were already pinned (`ComDispatcherTests`,
  `ComPlumbingTests`, `VsDiscoveryTests`); filter retry-window constants
  (3000/100) stay code-documented only — a unit test would reach into the
  private filter via reflection, against the seam rule.
- Remaining human step: live `BusyVs` path verified manually once — open a
  modal dialog in VS 2026 and confirm a tool call surfaces `vs-busy`, then
  dismiss and confirm recovery. Cannot be done headless; do it in the next
  real-VS pass and flip this issue to `resolved`.
- Build clean, 312 unit tests pass (311 + 1 new), E2E skipped (no live VS).
