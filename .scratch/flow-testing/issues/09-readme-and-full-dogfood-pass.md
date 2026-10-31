# 09: README section and full dogfood pass

**What to build:** `README.md` gains a short "Flow testing" section introducing the three skills. The full dogfood checklist from the spec's Testing Decisions runs end to end against real VS from an installed copy of the skills, and the results are recorded in this file under `## Comments` with a verdict. See [spec](../spec.md).

**Blocked by:** 02 (Ship skills in release and installer), 05 (command and human triggers, attach targets), 06 (Stale plans), 07 (Area and "all" requests), 08 (Multi-solution: cross-solution flows and parallel runs)

**Status:** done

- [x] `README.md` "Flow testing" section: what the skills do, setup first, one example request each
- [x] Skills installed via the installer (not copied by hand) before the pass
- [x] Every Testing Decisions dogfood step run and recorded with evidence
- [x] Any FAIL triaged into its own issue under this spec
- [x] Decide whether the checklist joins `definition-of-done.md`; record the decision here

## Comments

Full pass (2026-10-30). **Result:** every checklist step ended in its expected outcome. Two issues were filed (10, 11): one cross-solution run missed a checkpoint once (unexplained, so 10 stays open), and the installer has the wiring bugs in 11. All reports named below are in the dogfood copy (`docs/flows/runs/`), not in this repo. The pass reused the Sandbox copy from 06 to 08, because its two solutions were already open in VS, and reset its flow-testing files rather than making a fresh copy. Parallel dispatch is shown by the orchestrator's dispatch, not by the reports.

**Install.** Ran `install.ps1 -ConfigOnly -Dest <repo> -Exe <current server exe>`, which printed `[skill] plan-flow`, `[skill] run-flow` and `[skill] setup-flow-testing` into `~/.claude/skills`. `diff -r` against `skills/run-flow` found no differences. Every subagent below read `planner.md` / `runner.md` from `~/.claude/skills`. The client configs it rewired were restored from copies taken just before (developer's choice), and the Windsurf file it created was removed. Finding: Claude Code was **not** wired, because `.claude.json` has keys that differ only in case. Filed as [11](11-installer-skips-claude-json-with-case-duplicate-keys.md).

**Seam 1, in the Sandbox copy used for 06 to 08** (`Sandbox.slnx` and `second/Second.slnx`, each open in its own VS instance). Flow-testing doc, flows directory, pointer line and gitignore entry were all deleted first.

- Setup: the draft was confirmed by the developer; the doc lists 2 solutions, the build of both, and 3 targets. The index is empty; `.gitignore` has 1 line and `CLAUDE.md` has 1 pointer line; after a rerun, still 1 and 1, and the index was kept.
- plan-flow, three requests dispatched in parallel:
  - area `calc` gave 2 plans (`step`, `step2`), both `launch`;
  - `greet` gave a `human` plan;
  - `greet-pair` gave a cross-solution `command` plan.
  - All 15 proposed values came back as questions; the developer answered, each planner was resumed in turn, and each returned `Index: written`, `Still proposed: none`. The index has 3 areas and 4 lines.
- `run-flow all` (index order):
  - step (Sandbox) and step2 (Second) were dispatched in one message. greet (Sandbox) and greet-pair (both solutions) queued. greet started when step returned; greet-pair started after greet and step2 returned.
  - step PASS, step2 PASS. greet returned `Waiting: <instruction verbatim>`; the developer had me write `Ada`, the runner was resumed with `done`, and greet PASSed (OBS: exit code not observable).
  - Each report's Cleanup shows its breakpoints removed (`list_breakpoints` `[]`) and the session detached. The return blocks held only verdict, report, stale flag and non-PASS rows.
- Cross-solution: the first greet-pair run was **FAIL**: sandbox2 passed, sandbox's checkpoint was never hit (`hitCount: 0`), and a stray `other1` breakpoint was observed. VS afterwards showed no sessions and no breakpoints. A clean rerun **PASSed**, but its flow runner added diagnostics to the plan's command. A third run (`greet-pair-20261030-221355.md`) **PASSed** both checkpoints over two VS instances with the command run as written (one `vs-busy` along the way). Filed as [10](10-cross-solution-checkpoint-not-reached-once.md). Between greet and greet-pair, `name.txt` from greet had to be deleted by hand: flows that share a file need a clean start, which the planner warned about.
- Stale: Tick's `sum` was renamed to `acc`, then step was rerun. The first attempt was BLOCKED "build fails" because my edit also broke `Add`; that was the correct precondition stop. After fixing the edit: BLOCKED "plan stale: checkpoint 1 anchor `return sum;` not found in `Scenarios.Tick`", `Stale: yes (checkpoints 1)`, and the re-anchor offer was made. The developer declined, and the code was reverted. Re-anchoring itself was proved in 06.
- After the pass: `list_sessions` is empty.

**Seam 2 (installer copy into a temp HOME).** A fake install held a stub exe plus `skills/`, and each temp HOME held `.claude.json` and an unrelated `my-own` skill.
- `install.ps1 -ConfigOnly` (with `USERPROFILE` and `APPDATA` set to temp folders): `[skill]` lines for all three, `my-own` untouched (content `keep`).
- `install.sh --config-only`: with only the Microsoft Store `python3` stub it aborts before the skill copy (added to 11). With a `python3` shim pointing at real Python: all three copied, `my-own` untouched, `diff -r` against `skills/run-flow` showed no differences. But its Python wiring followed `USERPROFILE` and wrote the **real** client configs. All four were restored from their `.bak` files, which were checked against copies taken at the start. Recorded in 11 as a testing hazard.

**definition-of-done decision: joins**, as one step: a change under `skills/` reruns this checklist with installed skills. It proved useful: this pass found two bugs (10, 11) that the per-issue dogfoods missed. It is scoped to `skills/` changes because it needs two VS instances and a developer at hand.
