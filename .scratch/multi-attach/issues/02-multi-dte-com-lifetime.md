Type: research
Status: resolved

## Question

How does multi-DTE lifetime behave on the single `ComDispatcher.cs` STA thread: per-session `ReleaseComObject` without disturbing sibling sessions, ROT display-name stability while 3 VS instances stay open (`VsDiscovery.cs`), dispatcher queue behavior under concurrent agent calls to different sessions, and what a bound VS exiting mid-session looks like from COM (so per-session `vs-not-running` reports correctly)?

## Answer

Resolved via research subagent; full findings at `.scratch/multi-attach/research/multi-dte-lifetime.md` on throwaway branch `research/multi-dte-lifetime` (pushed, never merged). Verdicts for ticket 03: per-session release is safe (distinct RCWs) but must move inside a dispatcher work item; ROT names are stable while instances stay open but liveness needs a probe COM call (PID recycling); keep the single STA dispatcher for v1 with small snapshot work items and document head-of-line blocking; dead VS maps to per-session `vs-not-running` via new `ComErrorMapper` cases (`0x80010108`, `0x800706BA`, plus `0x800706B5`, `0x8001010A`) with the session record kept as `{ sessionId, displayName, pid, dead }` so later calls report `vs-not-running` rather than `no-session`. Refinement over ticket 01: keep dead records (dte null) instead of deleting them at detection time; `detach` is what removes a record.
Labels: wayfinder:research

## Question

How does multi-DTE lifetime behave on the single `ComDispatcher.cs` STA thread: per-session `ReleaseComObject` without disturbing sibling sessions, ROT display-name stability while 3 VS instances stay open (`VsDiscovery.cs`), dispatcher queue behavior under concurrent agent calls to different sessions, and what a bound VS exiting mid-session looks like from COM (so per-session `vs-not-running` reports correctly)?
