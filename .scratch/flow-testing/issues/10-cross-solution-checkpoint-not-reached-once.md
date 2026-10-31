# 10: Cross-solution run: one target's checkpoint not reached, once

**What happened:** In the issue 09 dogfood pass, the first `greet-pair` run (two debug sessions, `command` trigger) ended FAIL: sandbox2's checkpoint passed, but sandbox reached `mode: design` with its breakpoint at `Scenarios.cs:153` showing `hitCount: 0`. The run also showed an `other1` breakpoint at the same line in the Sandbox session, which the run did not create; `list_breakpoints` at cleanup showed none. A clean rerun of the same plan passed. Report: `docs/flows/runs/greet-pair-20261030-215101.md` in the dogfood copy. See [spec](../spec.md).

**Blocked by:** none

**Status:** needs-info

- [ ] Reproduce, or rule out: is it the flow runner's session switching (runner.md step 5, several debug sessions), the bridge, or the target exiting early?
- [x] Where did `other1` come from, and why was it gone by cleanup?
- [ ] Fix, or document the limit in runner.md

## Comments

Context: greet ran just before on the same Sandbox VS instance and left `name.txt` (Ada); it was deleted before greet-pair started, so both targets should have waited for the command. In the rerun, the flow runner appended diagnostics to the plan's command (`; echo exit=$?; cat name.txt`), which breaks runner.md step 5 ("character for character"). It reported that as an OBS. Watch for this. A third run (`greet-pair-20261030-221355.md`) PASSed with the command run as written. In that run the first `run_to_cursor` in the Sandbox debug session returned `vs-busy` (HRESULT 0x80010001) while that session was already in `mode: break` at the checkpoint. A busy VS around the session switch is a lead for the first run's miss.

Triage, 2026-10-30 (live, against the dogfood VS instances):

- `other1` is the run's own `bp1`, reproduced. Set a breakpoint, launch, run to the end, launch again: `list_breakpoints` shows `bp1` and `other1` at the same line with equal `hitCount` (both 0 before the hit, both 1 after), and `remove_breakpoint bp1` removes both, which is why cleanup listed none. VS hands the same breakpoint back through a new COM wrapper on relaunch, so the IUnknown identity match in `BreakpointKeeper` failed and the keeper listed it a second time. Fixed: ownership falls back to exact file:line:column (`BreakpointKeeper.IsOwned`). `--live-check-launch` now launches twice and fails if the breakpoint lists twice (baseline: `bp1@6 hits=1, other1@6 hits=1`; fixed: `bp1@6 hits=1`). `--live-check-occupied` still passes.
- The miss is not reproduced. Four greet-pair-shaped runs hit line 153 (fresh, after a no-op build, after a forced recompile, and after a relaunch). 40 unattended `Sandbox.exe greet` runs racing `echo Eve > name.txt` all exited 0, so a read/write race crashing the target is unlikely. `run_to_cursor` reuses the existing breakpoint at its destination and never deletes it, so it is ruled out too. The duplicate `other1` is a listing artifact, not a second breakpoint, so it does not explain the miss. Open lead: the target may have been launched with the wrong args (no `greet` runs `loop`, which ends in about 5 s without reaching `Greet`). The report did not record them, so runner.md now has a "not reached" row quote the target's `launch_process` args and `processId`.
- Coverage: `AuditE2ETests.StableIds_RelaunchListsOwnBreakpointOnce` (fails on the old keeper, passes with the fix). `StableIds_BreakpointIdentity` already fails on HEAD: removing an id that is already gone returns a generic invoke error, not `breakpoint-not-found`. That is not this change; it should be its own issue.
- Still open: the miss itself, and the `vs-busy` lead (not investigated). The runner.md change only collects evidence and documents no limit. The dogfood checklist rerun (definition-of-done #5) was not done for that one clause.
- Status `needs-info`: reopen with the new evidence if it recurs.
