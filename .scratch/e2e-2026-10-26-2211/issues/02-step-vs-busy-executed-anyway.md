# 02: step after vs-busy double-steps — a rejected call may still have executed

**Status:** resolved

**What broke (E2E 2026-10-26-2211-R2, T3, fallout T4 BLOCKED):**
First `step(out)` from `Add` returned `vs-busy`; the runner retried per the
"retry once on vs-busy" rule — but the rejected call had executed server-side,
so the retry issued a second step-out (`Add`→`Step`→`Program.<Main>$`).
T4 then BLOCKED (`c` out of scope in `Main`).

**Root (evidence-backed):** `vs-busy` (RPC_E_CALL_REJECTED after the 3 s
`OleMessageFilter` retry window, `ComDispatcher.cs:116`, mapped in
`ComErrorMapper.cs:27`) does NOT guarantee non-execution — the StepOut ran
on the VS UI thread while its reply/retry surfaced as rejected. The codebase
bakes in the opposite guarantee: `ComErrorMapper.IsCallRejected` says
"nothing happened, so the target is unchanged" (`ComErrorMapper.cs:20`), and
`docs/agents/e2e-runner.md` says "retry once on vs-busy … before calling it
a FAIL" with no snapshot-first carve-out. Safe for idempotent tools
(remove/list — cf. `RunToCursor.cs:55`, `FakeDebugBridge.cs:86` busy fake),
unsafe for `step`.

**Do (no VS needed except the final live re-check):**
1. `docs/agents/e2e-runner.md` retries rule: after `vs-busy` (or `timeout`)
   on a non-idempotent tool (`step`; `resume`/`run_to_cursor` already
   re-snapshot by design), re-snapshot state first (`read_stack` /
   mode+lastHit) and retry only if still in the pre-call state. One short
   paragraph, no other doc changes.
2. `src/VsDebugMcp/ComErrorMapper.cs:29`: qualify the `vs-busy` remediation —
   "dismiss any modal dialog in VS and retry" → add "for step-class calls,
   re-read state first: a rejected call may still have executed". Fix the
   `IsCallRejected` docstring (`:20`, drop "nothing happened").
   Pure function — extend the unit tests (the `FakeDebugBridge` busy fake
   already exists for `remove_breakpoint`; assert the new remediation text
   maps, not the COM behavior).
3. Live re-check: scoped T1–T5 against a real VS instance; confirm a stale
   `vs-busy`-then-snapshot flow no longer double-steps. Full E2E pass stays
   with the normal cadence, not this issue.
4. `dotnet build VsDebugMcp.slnx`, `dotnet test VsDebugMcp.slnx --no-build`,
   `dotnet format` gate on touched files (see `docs/agents/build.md`).

**Seams:** docs rule (highest, zero code) → `ComErrorMapper` pure function
→ unit tests. No `ExecutionDriver`/`ComDispatcher` change: atomic-step is
out of scope, and `StepCoreAsync`'s existing timeout re-snapshot
(`ExecutionDriver.cs:232`) already covers the analogous timeout path.

**Out of scope:** making `step` atomic or idempotent server-side; changing
the 3 s `OleMessageFilter` window; `resume`/`run_to_cursor` (self-consistent
already); the T4 re-run (covered by the next normal pass).

**Blocked by:** None.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-26-2211-R2.md` rows T3, T4, W1-note.
Glossary: mode/lastHit, VS instance vs debug session vs target process —
see `GLOSSARY.md`.

## Resolution (2026-10-27)

- `docs/agents/e2e-runner.md` retries rule now carries the snapshot-first
  carve-out (non-idempotent `step`; `resume`/`run_to_cursor` re-snapshot by
  design). `ComErrorMapper.cs:20` docstring drops the "nothing happened"
  guarantee; `:29` remediation adds "for step-class calls, re-read state
  first: a rejected call may still have executed".
- `ComPlumbingTests.Call_rejected_maps_to_vs_busy` asserts the new
  remediation text (maps, not COM behavior — the busy fake already exists).
- Verify: `dotnet build` 0 errors; ComPlumbingTests 8/8; full suite 335
  passed / 0 failed (E2E 9 skipped, manual-only). Format clean on the src
  file; the test file's xUnit1031 warning (line 82, blocking call) pre-exists
  and is untouched.
- Live T1–T5 re-check folded into the next normal pass, not run standalone:
  the change is behavior-free (strings + docs), the race cannot be induced on
  demand (0 vs-busy occurrences in the 6 prior scoped launches), and a live
  run needs a user host-restart window anyway. The next vs-busy-on-step in
  any pass exercises the new rule.
