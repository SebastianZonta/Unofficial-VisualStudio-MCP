# 01: C10 logpoint loses one first-iteration hit (bind race, not the stop-race)

**Status:** resolved

**Resolution (scoped C10 re-runs, 2026-10-27, same binary `standalone-e2e-2026-10-26-2211`, repo Sandbox, WORKER=83):**
6/6 launches ended `design` with no stop (issue-10 stop-race canary did NOT trip — 0 stops in 6, 1 stop in 10 launches total since 2026-10-24).
5/6 runs logged full 20/20 contiguous `1`…`20` with every pair `1/1`…`4/5` exactly once.
1/6 (bp6) logged 19 entries, hitCounts contiguous `1`…`19`, missing pair `4/1` — thread 4's FIRST iteration, same signature as the R1 miss (`2/1`).
Zero hitCount gaps in all 7 launches (R1 + 6 scoped): the VS counter never fired-without-logging, so no `TryLogAndContinueAsync` / `PollResumeAsync` settle loss.
Distribution matches the ticket's first branch exactly: missing pair always an early (`n=1`) pair with contiguous counts → launch bind race (breakpoint not yet armed when the first thread reaches WORKER). No product change; E2E-side handling added as the C10 flake rule in `docs/agents/e2e-phases-setup.md` (re-run once on 19-contiguous-early-missing; PASS on 20/20 re-run). Issue 10's stop-race canary stays as-is.
Note: `list_breakpoints` post-run reports the owned logpoint's `hitCount: 0` (VS resets `CurrentHits` at run end), so the `HitsOf`-vs-Log comparison is only meaningful mid-run; the decision above rests on contiguity + missing-pair position, which is conclusive for bind-race vs settle-loss.

**What broke (E2E 2026-10-26-2211-R1, C10):**
`launch_process("threads")` with logpoint `{id}/{n}` at WORKER → launch ended
`design` with no stop (the issue-10 stop-race canary did NOT trip), but the
session Log holds 19 entries for the logpoint id with contiguous hitCount
1…19; pair `2/1` is missing. No retry (assertion, not vs-busy/timeout).

**Why this is a new wrinkle, not the issue-10 stop-race:**
Contiguous hitCounts 1…19 mean VS's own hit counter saw exactly 19 hits — the
missing hit never fired the breakpoint at all (had it fired and only the
emulation dropped it, the counter would show a gap, e.g. 1…14,16…20).
And the missing pair is `2/1`: thread id 2's FIRST iteration — the classic
signature of a breakpoint that was not yet fully bound/armed when an early
thread reached WORKER at launch. Contrast with issue 10's Run-3 wrinkle
(19/20 plus one `(unavailable)/(unavailable)` = evaluator timing on a hit
that DID fire). The stop-race itself is now 1 stop in 4 runs.

**Do (agent, live VS required, same binary or newer):**
1. Scoped C10 re-runs, N ≥ 5, same shape as e2e-phases-debug.md C10
   (logpoint `{id}/{n}` at WORKER, `launch_process` `threads`, run to end).
2. Per run record: stop or clean `design`?; Log entry count for the logpoint
   id; hitCount contiguity (contiguous N vs gap); which pair is missing;
   `BreakpointKeeper.HitsOf` (VS counter) vs Log count at end.
3. Decide by the distribution:
   - Missing pair always an early (`n=1`) pair with contiguous counts →
     launch bind race (VS behavior, breakpoint not armed for the first
     thread through). No product fix: fix the E2E expectation (tolerance /
     re-run) and close this issue, keeping issue 10's canary for the
     stop-race only.
   - Gaps in hitCounts (counter fired, entry missing) → emulated-logpoint
     loss in `TryLogAndContinueAsync` / `PollResumeAsync` settle
     (`DteDebugBridge.cs:448`, `ExecutionDriver.cs:133`,
     `LogpointResumeSettled` at `DteDebugBridge.cs:537`) → full
     PollResume-race investigation as a follow-up issue.
4. Append the per-run table to this file under `## Comments` and set
   `Status:` accordingly (`resolved` if bind race with E2E-side handling,
   else a new investigation issue linked here).

**Seams (highest first, only if the distribution points at product code):**
`TryLogAndContinueAsync` settle window (`DteDebugBridge.cs:516`) is the only
product seam; prefer no product change — the leading hypothesis needs none.

**Out of scope:** the issue-10 stop-race (separate canary, did not reproduce);
C6-style single-thread logpoints (20/20 PASS this run).

**Blocked by:** None.

## Comments

E2E evidence: `.scratch/e2e-runs/2026-10-26-2211-R1.md` rows C10, O1.
Related: `.scratch/e2e-2026-10-24/issues/10-logpoint-hit-surfaces-as-stop.md`
(needs-info canary; this run: no stop, 1 stop in 4 runs total).
Glossary: logpoint (emulated evaluate-then-decide with auto-continue),
lastHit, debug session, target process — see `GLOSSARY.md`.

### Scoped C10 re-runs (2026-10-27, VS 18.0 Community pid 13916, session `a46f55…`, binary `standalone-e2e-2026-10-26-2211`)

Shape per run: set logpoint `{id}/{n}` at `Scenarios.cs:83` (WORKER), `launch_process(Sandbox.exe, "threads")` to end, `list_breakpoints` for Log + owned `hitCount`, then remove owned id (clears the transient post-run-end `other1` twin each time — benign, already handled).

| Run | bp | Launch end | Log count (this id) | hitCount range | Contiguous? | Missing pair | Owned `hitCount` after end | Twin |
|---|---|---|---|---|---|---|---|---|
| R1 C10 (orig) | bp13 | `design`, no stop | 19 | 1…19 | yes | `2/1` | 19 at capture* | `other1` transient |
| scoped 1 | bp1 | `design`, no stop | 20 | 1…20 | yes | — | 0 (reset) | `other1` transient |
| scoped 2 | bp2 | `design`, no stop | 20 | 1…20 | yes | — | 0 (reset) | `other1` transient |
| scoped 3 | bp3 | `design`, no stop | 20 | 1…20 | yes | — | 0 (reset) | `other1` transient |
| scoped 4 | bp4 | `design`, no stop | 20 | 1…20 | yes | — | 0 (reset) | `other1` transient |
| scoped 5 | bp5 | `design`, no stop | 20 | 1…20 | yes | — | 0 (reset) | `other1` transient |
| scoped 6 | bp6 | `design`, no stop | 19 | 1…19 | yes | `4/1` | 0 (reset) | `other1` transient |

\* R1 row read `hitCount` from the failure table (`{bp11:20, bp13:19}` = Log counts); scoped runs confirm the live `hitCount` field reads 0 after run end (VS counter reset), so only Log contiguity discriminates.
Session Log total after 6 scoped runs: 119 entries (20+20×4+19), consistent — no cross-run leakage.
