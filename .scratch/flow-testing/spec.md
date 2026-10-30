# Flow testing skills

Status: ready-for-agent

## Problem Statement

A developer using this MCP on their own app can drive the VS debugger from an
agent, but every check of an app behaviour ("POST /orders applies the
discount") starts from zero: the agent has to rediscover the solution to bind,
how to build and start the target process, where to stop, what to read, and
what the right values are. Nothing written down survives the conversation, so
the same flow is re-explored by hand every time, the main conversation fills
with debugger output, and when the app is several services in several
solutions the agent has no shared picture of which VS instance owns which part
of the flow.

## Solution

Three skills ship with the MCP and are installed at user level next to the
MCP registration:

- **setup-flow-testing**: run once per developer repo, in the main
  conversation with the developer. It records how to reach the app under the
  debugger (solutions, VS instance overrides, build command, named targets and
  how each is launched or attached, where flow plans and run reports live, the
  elevation rule), creates an empty flow index, gitignores the report
  directory, and adds one pointer line to the repo's `CLAUDE.md` or
  `AGENTS.md`.
- **plan-flow**: the developer names a flow or an area; a planner subagent
  reads the code (never the debugger) and writes or updates committed flow
  plans, each with a trigger, ordered checkpoints, and expectations. Every
  expectation it reads off the code comes back as *proposed*; the main agent
  puts those to the developer and resumes the same planner to finish.
- **run-flow**: the developer names a flow, an area, or "all"; the main agent
  resolves them through the flow index, groups them by the solutions they
  touch, and dispatches one flow runner subagent per flow (in parallel only
  when their solutions are disjoint). Each flow runner drives the debugger,
  writes a full report, and returns only the verdict, the report path, and the
  FAIL/BLOCKED/OBS rows.

The main agent is only ever the orchestrator; debugger output and code reading
stay inside subagents.

## User Stories

1. As a developer, I want the flow-testing skills installed by the same installer that registers the MCP, so that I get them without a separate step.
2. As a developer, I want the skills installed at user level, so that they are available in every repo I open.
3. As a developer, I want the installer to ship the skills inside the checksummed release bundle, so that no unverified file is fetched.
4. As a developer, I want re-running the installer to replace the three skill folders with the shipped version, so that I always run the skills matching my MCP.
5. As a developer, I want the installer to leave every other skill in my user skill directory untouched, so that my own skills survive an upgrade.
6. As a developer, I want `setup-flow-testing` to explore my repo before asking anything, so that it proposes solutions and projects it actually found.
7. As a developer, I want setup to confirm its draft with me before writing, so that nothing lands in my repo I did not approve.
8. As a developer, I want setup to record each solution and its VS instance override, so that flow runners bind the right VS instance.
9. As a developer, I want setup to record the build command, so that flow runners test the code I have, not a stale build.
10. As a developer, I want setup to record named targets, each started by `launch` with an executable path or found by `attach` with a process name, so that plans refer to services by name.
11. As a developer of a microservices app, I want several targets across several solutions recorded, so that one flow can follow a request across services.
12. As a developer, I want setup to state that VS and the MCP host must run at the same elevation, so that a blocked run explains itself.
13. As a developer, I want setup to ask once for the run report directory, defaulting to the flows' runs directory, so that reports land where I want.
14. As a developer, I want the chosen report directory gitignored, so that run reports never pollute commits.
15. As a developer, I want setup to create an empty flow index, so that the first plan has somewhere to be listed.
16. As a developer, I want setup to add one line to whichever of `CLAUDE.md` / `AGENTS.md` already exists, so that every agent in my repo finds the flow-testing doc.
17. As a developer, I want setup never to create `AGENTS.md` when `CLAUDE.md` exists (or the reverse), and to ask when neither exists, so that my agent-instruction layout is respected.
18. As a developer, I want re-running setup to update its doc and pointer in place, so that I never get duplicate sections.
19. As a developer, I want `plan-flow` to refuse to run before setup and tell me to run setup, so that plans never invent targets.
20. As a developer, I want to name one flow in plain words, so that the planner finds its entry point in the code itself.
21. As a developer, I want to name an area ("the checkout flows") and get several flow plans under that area, so that I can cover a feature in one request.
22. As a developer, I want a request for a flow that already has a plan to update that plan, so that the index never holds duplicates.
23. As a developer, I want each flow plan to name its trigger as `launch`, `command`, or `human`, so that the run knows what makes the code execute.
24. As a developer, I want each checkpoint anchored by file, method, and a short verbatim code fragment, so that plans survive edits that move lines.
25. As a developer, I want each checkpoint to name its target, so that a cross-service flow knows which VS instance owns it.
26. As a developer, I want each checkpoint to list the expressions to read and their expected values, so that the run has something to judge.
27. As a developer, I want the plan's expected outcome stated, so that the run judges the flow end to end, not just its stops.
28. As a developer, I want every expectation the planner infers from code marked *proposed*, so that the code is never tested against itself.
29. As a developer, I want the planner's open questions put to me by the main agent, so that I answer them in my own conversation.
30. As a developer, I want my answers sent back to the same planner, so that it finishes the plan without re-reading the code.
31. As a developer, I want the planner never to touch the debugger, so that planning works with VS closed and costs no debugger time.
32. As a developer, I want every new or updated plan listed in the flow index with a one-line gist, grouped by area, so that I can see all my flows at a glance.
33. As a developer, I want flow plans committed, so that teammates and later agents rerun them as regression checks.
34. As a developer, I want flow plans never to record run results, so that a plan diff only ever means an intent change.
35. As a developer, I want `run-flow` to accept a flow name, an area, or "all", so that I can run one check or a whole suite.
36. As a developer, I want a name missing from the flow index to be an error, so that nothing guesses which plan I meant.
37. As a developer, I want a plan with any proposed expectation refused before it runs, so that only confirmed intent is checked.
38. As a developer, I want each flow run by its own flow runner subagent, so that debugger output never floods my conversation.
39. As a developer with one solution, I want flows run one after another, so that runners never fight over one VS instance.
40. As a developer with several solutions, I want flows whose solutions are disjoint run in parallel, so that a suite finishes sooner.
41. As a developer, I want flows sharing a solution queued, so that parallelism never costs correctness.
42. As a developer, I want the flow runner to check its preconditions (VS running with the solution open, build green, target reachable) before setting anything, so that a broken environment is BLOCKED, not FAIL.
43. As a developer, I want the flow runner never to start VS, so that elevation mismatches are reported rather than worked around.
44. As a developer, I want every BLOCKED to carry the fix, so that I know what to do next.
45. As a developer, I want the flow runner to build with the recorded command before running, so that the run reflects my current code.
46. As a developer, I want `launch` targets started attached from birth, so that startup checkpoints hit.
47. As a developer, I want `attach` targets found by their recorded process name and attached with consent, so that the plan's naming of the target is the consent.
48. As a developer, I want a `command` trigger executed by the flow runner exactly as the plan states, so that HTTP or CLI entry points need no human.
49. As a developer, I want a `human` trigger to pause the run while the main agent asks me to act, so that UI flows still work.
50. As a developer, I want the flow runner never to invent a trigger the plan does not name, so that runs stay reproducible.
51. As a developer, I want each checkpoint's current line found from its anchor before setting the breakpoint, so that the line number in the plan is only a hint.
52. As a developer, I want a vanished anchor to make the run BLOCKED with "plan stale", so that a run never checks the wrong code.
53. As a developer, I want a stale plan to produce an offer to re-anchor through the planner, never an automatic rewrite, so that what the flow checks never changes silently.
54. As a developer of a microservices app, I want the flow runner to hold one debug session per VS instance the flow touches, so that it follows a request across services.
55. As a developer, I want the flow runner never to mutate target state, so that a flow only observes.
56. As a developer, I want every checkpoint judged PASS, FAIL, BLOCKED, or OBS with quoted tool evidence, so that the verdict is checkable.
57. As a developer, I want surprises outside any expectation recorded as OBS, so that they are not lost and do not fail the run.
58. As a developer, I want the verdict PASS only when nothing FAILs or BLOCKs, so that PASS means what it says.
59. As a developer, I want each run's full report written to the report directory, named by flow and run tag, so that I can read the detail later.
60. As a developer, I want to name a different report location in my run request, so that one run can land elsewhere without changing setup.
61. As a developer, I want each flow runner to return only verdict, report path, and the non-PASS rows, so that my conversation stays small.
62. As a developer, I want the flow runner to detach every debug session it opened and leave targets as it found or launched them on every exit, so that the next run starts clean.
63. As a developer, I want the skills to use the MCP's own vocabulary (VS instance, debug session, target process, verdict), so that skill text, tool results, and docs agree.

## Implementation Decisions

- **Three skills, one orchestration rule.** `setup-flow-testing` runs in the main conversation (it talks to the developer). `plan-flow` and `run-flow` are main-agent orchestrators whose work is done by subagents; each ships its subagent instructions as a reference file inside its skill folder, read by the subagent, not loaded into the main conversation.
- **Skill sources live in this repo** in a top-level skills directory, one folder per skill. The release workflow adds that directory to the release zip, so the skills ride the existing checksum.
- **Installer**: after extracting the bundle, when Claude Code is detected (the existing client probe), copy the three skill folders into the user-level Claude skill directory, replacing only those three folders. `-ConfigOnly` also performs the copy from an existing install so it can be exercised without a download. Both `install.ps1` and `install.sh` behave the same.
- **Developer-repo layout written by setup**: a flow-testing doc in the repo's agent-docs directory; a flow index and per-area flow plans under a committed flows directory; a run report directory (default inside the flows directory) that setup gitignores; one pointer line in the existing `CLAUDE.md` or `AGENTS.md` (Matt Pocock setup rule: edit whichever exists, never create the other, ask when neither exists, update in place on rerun).
- **Flow-testing doc contents**: solutions with their VS instance override; the build command; named targets, each with its solution and either `launch` (executable path, arguments, working directory) or `attach` (process name); flows directory; report directory; the same-elevation rule.
- **Flow plan contents**: name and area; one-line gist; trigger (`launch`, `command` with the exact command, or `human` with the instruction to give); ordered checkpoints, each with target name, file, method, verbatim anchor fragment, a line hint, expressions to read, and expectations each marked *proposed* or *confirmed*; the expected outcome.
- **Flow index**: grouped by area, one line per flow plan (name linking the plan, gist). Never restates plan content.
- **Planner subagent** (sonnet): code-reading only, no MCP calls. Input: the request, the flow-testing doc, the flow index. Output: the plan files it wrote or updated, index lines, and a list of open questions (proposed expectations, ambiguities). The main agent relays questions to the developer and resumes the same planner via SendMessage with the answers; the planner then marks expectations confirmed and finalises.
- **Run orchestration**: resolve requested names through the flow index (unknown name = error, no fuzzy match); refuse any plan with a proposed expectation; compute each flow's solution set from its checkpoints' targets; dispatch flow runners so that concurrently running flows have disjoint solution sets, queueing the rest.
- **Flow runner subagent** (sonnet): reads the flow-testing doc and one plan. Order: preconditions (VS instance for each solution is listed by `list_instances` with the solution open; build command green), bind each target (one debug session per VS instance; `launch_process` or `list_processes` + `attach_process` by recorded name with `consent: true`), resolve each checkpoint's anchor to a current line (vanished anchor = BLOCKED "plan stale"), set breakpoints, fire the trigger (`human`: return to the main agent asking for the action, then be resumed), at each hit evaluate the plan's expressions and judge, then clean up (remove its breakpoints, detach every debug session) on every exit path. Never `set_variable`, never start VS.
- **Run report**: same status vocabulary and evidence rule as the E2E report (PASS/FAIL/BLOCKED/OBS per checkpoint, quoted tool output), named `<flow>-<run tag>`, written to the report directory or the location named in the run request.
- **Return contract to the main agent**: verdict, report path, the FAIL/BLOCKED/OBS rows, and a stale-plan flag; nothing else.
- **This repo's docs**: `INSTALL.md` notes the installer copies the skills; `README.md` gets a short "Flow testing" section; `AGENTS.md` gets one line pointing at the skills directory. Glossary terms (flow, flow plan, flow index, checkpoint/expectation, trigger, flow runner) are already in `GLOSSARY.md`.

## Testing Decisions

- A good test here exercises the skills the way a developer would, through the real MCP against real VS, and judges only what lands on disk and what the main agent receives back; never the wording of a skill.
- **Seam 1: dogfood run.** Copy `Sandbox/` to a temp directory and treat it as a developer repo. Run `setup-flow-testing`; check the doc, the empty flow index, the gitignore entry, and the single pointer line (rerun setup: still one line). Run `plan-flow` for two flows, one `launch`-triggered and one `human`-triggered; check proposed expectations come back as questions and the resumed planner marks them confirmed. Run `run-flow` on both; check verdicts, report files, the compact return, and that no breakpoint or debug session is left behind. Edit an anchor away and rerun: BLOCKED "plan stale" plus the re-anchor offer. Run once with a cross-solution flow over two VS instances (a second solution wrapping a Sandbox copy), and once with two disjoint flows to see them dispatched in parallel. Record results as an issue under this spec with a `Status:` line.
- **Seam 2: installer copy.** Run `install.ps1 -ConfigOnly` and `install.sh` against a temp HOME with a fake existing install containing the skills directory plus an unrelated pre-existing user skill; check the three skill folders landed and the unrelated skill is untouched.
- Prior art: the E2E pass (`docs/agents/e2e.md`, `e2e-runner.md`) for subagent fan-out, report format, and evidence rules; the multi-attach work for several debug sessions in one client session.
- Proven capabilities: subagents calling MCP tools (E2E runners), several debug sessions per client session (multi-attach), user-level skill discovery (existing installed skills), resuming a completed subagent with its context via SendMessage (probed 2026-10-30: resumed agent recalled its first-turn token).
- The dogfood checklist joins `definition-of-done.md` only after it has proved useful.

## Out of Scope

- Packaging as a Claude Code plugin, and opencode or other clients' skill directories.
- Flows that mutate target state (`set_variable`) or change exception settings as part of a check.
- The flow runner starting or restarting VS, or fixing elevation.
- Automatic re-anchoring of stale plans.
- Parallel runs of flows that share a solution (would need extra VS instances per runner).
- A wayfinder-style decision map; the flow index is the only cross-flow artifact.
- CI execution of flow plans.
- Changes to the MCP server's tools.

## Further Notes

- Glossary terms added during the grilling: **Flow / flow plan**, **Checkpoint / expectation**, **Flow index**, **Trigger**, **Flow runner**; **Verdict** widened to cover a flow run. The existing E2E **Runner** keeps its meaning; always say "flow runner" for the new role.
- No ADR: the choices are reversible or not the result of a hard trade-off.
